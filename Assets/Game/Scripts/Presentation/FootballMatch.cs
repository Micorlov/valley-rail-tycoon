using System.Collections.Generic;
using UnityEngine;

namespace ValleyRail
{
    /// <summary>
    /// A cosmetic football match: two teams dribble, pass and shoot, keepers save, goals go up on the scoreboard and a new
    /// match kicks off after full time. Every match ends <see cref="FinalScore"/>: the goals are planned at kick-off and go
    /// in with the first shot a team takes once one is due. It works in the pitch frame (x between the goals, z across, origin on the centre
    /// spot) with its own random numbers, so it never touches the simulation.
    /// </summary>
    public sealed class FootballMatch
    {
        public enum Phase { Kickoff, Play, Goal, FullTime }
        public enum Role { Keeper, Outfield, Referee }
        public sealed class Player
        {
            public int team; // 0 or 1; -1 for the referee
            public Role role;
            // Formation spot: x from -1 (own goal line) to +1 (their goal line), y from -1 to 1 across the pitch.
            public Vector2 home;
            public Vector2 position, velocity;
            public float facing; // yaw in degrees
            public float stride; // running cycle, in steps
            public float hop; // jump height while celebrating
            public float stunned; // seconds left on the grass after losing a tackle
        }
        enum Kick { Pass, Shot }
        enum ShotEnd { Goal, Save, Wide }
        /// <summary>Real seconds per match at 1× speed; the scoreboard clock runs 90 minutes in this time.</summary>
        public const float MatchSeconds = 180;
        const float GoalPause = 4.5f, FullTimePause = 12, KickoffWait = 1.5f, KickoffGiveUp = 6;
        /// <summary>How every match ends: the yellow-and-black home side (team 0) beats red-and-black 8–2.</summary>
        public static readonly int[] FinalScore = { 8, 2 };
        // All goals go in within this share of the match, so the final score stays up for the rest of it; a new stadium
        // joins its first match this far in, already at the final score.
        const float GoalsFrom = .03f, GoalsUntil = .5f, JoinAt = .72f;
        // Keeper, back four, midfield four and two strikers; small grounds play six-a-side.
        static readonly Vector2[] Eleven =
        {
            new Vector2(-.93f, 0), new Vector2(-.58f, -.62f), new Vector2(-.64f, -.22f), new Vector2(-.64f, .22f), new Vector2(-.58f, .62f),
            new Vector2(-.18f, -.6f), new Vector2(-.26f, -.2f), new Vector2(-.26f, .2f), new Vector2(-.18f, .6f), new Vector2(.22f, -.24f), new Vector2(.22f, .24f),
        };
        static readonly Vector2[] Six = { new Vector2(-.93f, 0), new Vector2(-.55f, -.4f), new Vector2(-.55f, .4f), new Vector2(-.15f, -.5f), new Vector2(-.15f, .5f), new Vector2(.22f, 0) };

        readonly System.Random random;
        readonly float halfLength, halfWidth, goalHalf, goalDepth, goalHeight, radius, reach;
        readonly float sprint, jog, dribble, passSpeed, shotSpeed;
        readonly int perTeam;
        readonly int[] pressers = new int[2];
        public readonly Player[] players;
        public readonly int[] score = new int[2];
        public Phase Stage { get; private set; }
        public float PhaseTime { get; private set; }
        /// <summary>Seconds played in the current match.</summary>
        public float Clock { get; private set; }
        /// <summary>Ball centre in the pitch frame: x and z on the grass, y its height above it.</summary>
        public Vector3 Ball { get; private set; }
        public int LastScorer { get; private set; } = -1;
        public int Goals { get; private set; }
        public int Shots { get; private set; }
        public int Saves { get; private set; }
        public int Passes { get; private set; }
        public int Tackles { get; private set; }
        public int Matches { get; private set; }
        public float JogSpeed => jog;
        public int Minute => Mathf.Clamp(1 + (int)(Clock / MatchSeconds * 90), 1, 90);
        /// <summary>The team whose fans are celebrating a goal right now, or -1.</summary>
        public int CheeringTeam => Stage == Phase.Goal ? LastScorer : -1;

        float time, decision, flightTime, flightDuration, flightArc, restart = -1;
        int owner = -1, receiver = -1, kicker = -1, scorer = -1, restartKeeper = -1, kickoffTeam;
        bool inFlight;
        Kick kick;
        ShotEnd shotEnd;
        Vector2 flightFrom, flightTo, roll;
        readonly List<(float time, int team)> goalPlan = new List<(float, int)>();
        int nextGoal;

        /// <param name="playerScale">Figure scale of the players; 1 is a pavement walker.</param>
        public FootballMatch(float length, float width, float goalWidth, float goalDepth, float goalHeight, float playerScale, bool elevenASide, int seed)
        {
            random = new System.Random(seed);
            halfLength = length / 2;
            halfWidth = width / 2;
            goalHalf = goalWidth / 2;
            this.goalDepth = goalDepth;
            this.goalHeight = goalHeight;
            radius = .03f * playerScale;
            reach = .06f * playerScale;
            sprint = length / 7.5f;
            jog = length / 15;
            dribble = length / 11;
            passSpeed = length / 2.4f;
            shotSpeed = length / 1.25f;
            var formation = elevenASide ? Eleven : Six;
            perTeam = formation.Length;
            players = new Player[perTeam * 2 + 1];
            for (int team = 0; team < 2; team++)
                for (int i = 0; i < perTeam; i++)
                    players[team * perTeam + i] = new Player { team = team, role = i == 0 ? Role.Keeper : Role.Outfield, home = formation[i] };
            players[players.Length - 1] = new Player { team = -1, role = Role.Referee };
            BeginKickoff(random.Next(2));
            for (int i = 0; i < players.Length; i++)
                players[i].position = KickoffSpot(i);
            PlanGoals();
            nextGoal = goalPlan.Count;
            score[0] = FinalScore[0];
            score[1] = FinalScore[1];
            Clock = MatchSeconds * JoinAt;
        }
        /// <summary>Shuffles who scores when, for a match that ends <see cref="FinalScore"/>.</summary>
        void PlanGoals()
        {
            goalPlan.Clear();
            nextGoal = 0;
            var scorers = new List<int>();
            for (int team = 0; team < 2; team++)
                for (int i = 0; i < FinalScore[team]; i++)
                    scorers.Add(team);
            for (int i = scorers.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (scorers[i], scorers[j]) = (scorers[j], scorers[i]);
            }
            var times = new List<float>();
            foreach (int _ in scorers)
                times.Add(Rand(GoalsFrom, GoalsUntil) * MatchSeconds);
            times.Sort();
            for (int i = 0; i < scorers.Count; i++)
                goalPlan.Add((times[i], scorers[i]));
        }
        bool GoalDue(int team) => nextGoal < goalPlan.Count && goalPlan[nextGoal].team == team && Clock >= goalPlan[nextGoal].time;

        /// <summary>Advances the match; keep steps short (a tenth of a second or less) for smooth running.</summary>
        public void Step(float dt)
        {
            time += dt;
            PhaseTime += dt;
            switch (Stage)
            {
                case Phase.Kickoff: StepKickoff(dt); break;
                case Phase.Play: StepPlay(dt); break;
                case Phase.Goal: StepGoal(dt); break;
                default: StepFullTime(dt); break;
            }
            Separate();
        }

        static float Dir(int team) => team == 0 ? 1 : -1;
        Vector2 Ball2 => new Vector2(Ball.x, Ball.z);
        Vector2 Spot(int team, Vector2 frame) => new Vector2(frame.x * halfLength * Dir(team), frame.y * halfWidth);
        int KeeperOf(int team) => team * perTeam;
        int StrikerOf(int team) => team * perTeam + perTeam - 1;
        int Possession => owner >= 0 ? players[owner].team : receiver >= 0 ? players[receiver].team : -1;
        float Rand(float from, float to) => from + (float)random.NextDouble() * (to - from);

        void BeginKickoff(int team)
        {
            Stage = Phase.Kickoff;
            PhaseTime = 0;
            kickoffTeam = team;
            owner = receiver = restartKeeper = -1;
            restart = -1;
            inFlight = false;
            roll = Vector2.zero;
            Ball = Vector3.zero;
            foreach (var p in players)
            {
                p.hop = 0;
                p.stunned = 0;
            }
        }
        Vector2 KickoffSpot(int i)
        {
            var p = players[i];
            if (p.role == Role.Referee)
                return new Vector2(-halfLength * .1f, -halfWidth * .3f);
            if (i == StrikerOf(kickoffTeam))
                return Spot(kickoffTeam, new Vector2(-reach * 1.2f / halfLength, 0));
            float x = p.role == Role.Keeper ? p.home.x : Mathf.Min(p.home.x * .9f, -.08f);
            return Spot(p.team, new Vector2(x, p.home.y));
        }
        void StepKickoff(float dt)
        {
            bool ready = true;
            for (int i = 0; i < players.Length; i++)
            {
                var target = KickoffSpot(i);
                float gap = Vector2.Distance(players[i].position, target);
                Steer(players[i], target, gap > halfLength * .25f ? sprint : jog, dt);
                ready &= gap < reach;
            }
            if (PhaseTime < KickoffWait || !ready && PhaseTime < KickoffGiveUp)
                return;
            Stage = Phase.Play;
            PhaseTime = 0;
            owner = StrikerOf(kickoffTeam);
            decision = .35f;
        }

        void StepPlay(float dt)
        {
            Clock += dt;
            FindPressers();
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p.stunned > 0)
                {
                    p.stunned -= dt;
                    Steer(p, p.position, 0, dt);
                    continue;
                }
                Choose(i, out var target, out float speed);
                Steer(p, target, speed, dt);
            }
            MoveBall(dt);
            // Full time waits for any planned goal still to come: stoppage time.
            if (Stage == Phase.Play && Clock >= MatchSeconds && !inFlight && nextGoal >= goalPlan.Count)
            {
                Stage = Phase.FullTime;
                PhaseTime = 0;
                owner = receiver = -1;
            }
        }
        /// <summary>One player per team closes the ball down: both chase a loose ball, only the defenders press a dribbler.</summary>
        void FindPressers()
        {
            pressers[0] = pressers[1] = -1;
            if (inFlight || restart >= 0 || owner >= 0 && players[owner].role == Role.Keeper)
                return;
            for (int team = 0; team < 2; team++)
                if (Possession != team)
                    pressers[team] = Nearest(Ball2, team, receiver);
        }
        void Choose(int i, out Vector2 target, out float speed)
        {
            var p = players[i];
            var ball = Ball2;
            if (p.role == Role.Referee)
            {
                // The referee trails play, a little to one side so he never stands on the ball.
                target = new Vector2(ball.x * .85f, ball.y > 0 ? ball.y - halfWidth * .45f : ball.y + halfWidth * .45f);
                speed = jog;
                return;
            }
            float dir = Dir(p.team);
            if (i == owner)
            {
                target = p.role == Role.Keeper ? p.position : DribbleTarget(p);
                speed = GoalDue(p.team) ? sprint * .85f : dribble;
                return;
            }
            if (i == receiver)
            {
                target = inFlight ? flightTo : ball;
                speed = p.role == Role.Keeper ? sprint * 1.5f : sprint;
                return;
            }
            if (p.role == Role.Keeper)
            {
                float line = -dir * halfLength;
                if (inFlight && kick == Kick.Shot && shotEnd == ShotEnd.Goal && flightTo.x * dir < 0)
                    target = new Vector2(line + dir * reach, -Mathf.Sign(flightTo.y) * goalHalf * .6f); // dives the wrong way
                else if (i == restartKeeper)
                    target = new Vector2(line + dir * halfLength * .1f, 0);
                else
                    target = new Vector2(line + dir * halfLength * .05f, Mathf.Clamp(ball.y * .35f, -goalHalf * .8f, goalHalf * .8f));
                speed = jog * 1.5f;
                return;
            }
            if (i == pressers[p.team])
            {
                target = ball;
                speed = sprint * .9f;
                return;
            }
            target = FormationSpot(p, i);
            speed = Vector2.Distance(p.position, target) > halfLength * .3f ? sprint * .8f : jog;
        }
        /// <summary>A player's spot as the team shifts with the ball: up the pitch when attacking, back when defending.</summary>
        Vector2 FormationSpot(Player p, int i)
        {
            float ahead = Ball.x * Dir(p.team) / halfLength;
            float x = Mathf.Clamp(p.home.x + ahead * .45f + (Possession == p.team ? .16f : -.1f), -.86f, .86f);
            float y = Mathf.Clamp(p.home.y * .85f + Ball.z / halfWidth * .22f + Mathf.Sin(time * .6f + i * 1.7f) * .05f, -.92f, .92f);
            return Spot(p.team, new Vector2(x, y));
        }
        Vector2 DribbleTarget(Player p)
        {
            float wander = Mathf.Sin(time * .8f + owner * 2.1f) * .3f * halfWidth;
            return new Vector2(Dir(p.team) * halfLength * .92f, Mathf.Clamp(p.position.y * .6f + wander, -halfWidth * .75f, halfWidth * .75f));
        }

        void Steer(Player p, Vector2 target, float speed, float dt)
        {
            var to = target - p.position;
            float gap = to.magnitude;
            var want = gap > 1e-4f ? to / gap * speed * Mathf.Clamp01(gap / (reach * 2)) : Vector2.zero;
            p.velocity = Vector2.MoveTowards(p.velocity, want, sprint * 3 * dt);
            p.position += p.velocity * dt;
            p.position = new Vector2(Mathf.Clamp(p.position.x, -halfLength - goalDepth, halfLength + goalDepth), Mathf.Clamp(p.position.y, -halfWidth - reach, halfWidth + reach));
            float moving = p.velocity.magnitude;
            p.stride += moving * dt / (radius * 3.2f);
            var look = moving > jog * .25f ? p.velocity : Ball2 - p.position;
            if (look.sqrMagnitude > 1e-6f)
                p.facing = Mathf.MoveTowardsAngle(p.facing, Mathf.Atan2(look.x, look.y) * Mathf.Rad2Deg, 720 * dt);
        }
        /// <summary>Players never stand inside one another.</summary>
        void Separate()
        {
            float apart = radius * 2.1f;
            for (int i = 0; i < players.Length; i++)
                for (int j = i + 1; j < players.Length; j++)
                {
                    var delta = players[j].position - players[i].position;
                    float gap = delta.magnitude;
                    if (gap >= apart || gap < 1e-5f)
                        continue;
                    var push = delta / gap * (apart - gap) * .5f;
                    players[i].position -= push;
                    players[j].position += push;
                }
        }
        /// <summary>The nearest outfield player of <paramref name="team"/> to a point, skipping the fallen and <paramref name="skip"/>.</summary>
        int Nearest(Vector2 point, int team, int skip = -1)
        {
            int best = -1;
            float bestGap = float.MaxValue;
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (i == skip || p.role != Role.Outfield || p.stunned > 0 || team >= 0 && p.team != team)
                    continue;
                float gap = (p.position - point).sqrMagnitude;
                if (gap < bestGap)
                {
                    best = i;
                    bestGap = gap;
                }
            }
            return best;
        }

        void MoveBall(float dt)
        {
            if (restart >= 0)
            {
                // A goal kick after a miss: the ball rests out of play, then the keeper has it.
                restart -= dt;
                if (restart < 0)
                {
                    owner = restartKeeper;
                    restartKeeper = -1;
                    decision = .8f;
                }
                return;
            }
            if (inFlight)
                Fly(dt);
            else if (owner >= 0)
                Carry(dt);
            else
                Roll(dt);
        }
        void Fly(float dt)
        {
            flightTime += dt;
            float u = Mathf.Clamp01(flightTime / flightDuration);
            var flat = Vector2.Lerp(flightFrom, flightTo, u);
            Ball = new Vector3(flat.x, flightArc * 4 * u * (1 - u), flat.y);
            if (u < 1)
                return;
            inFlight = false;
            if (kick == Kick.Shot)
            {
                EndShot();
                return;
            }
            if (receiver >= 0 && Vector2.Distance(players[receiver].position, flightTo) < reach * 2.2f)
            {
                owner = receiver;
                receiver = -1;
                decision = Rand(.5f, 1.8f);
            }
            else
                roll = (flightTo - flightFrom).normalized * passSpeed * .35f; // runs loose; the receiver keeps chasing
        }
        void Roll(float dt)
        {
            var at = Ball2 + roll * dt;
            roll = Vector2.MoveTowards(roll, Vector2.zero, halfLength * .9f * dt);
            if (Mathf.Abs(at.x) > halfLength - reach)
            {
                at.x = Mathf.Sign(at.x) * (halfLength - reach);
                roll.x *= -.4f;
            }
            if (Mathf.Abs(at.y) > halfWidth - reach)
            {
                at.y = Mathf.Sign(at.y) * (halfWidth - reach);
                roll.y *= -.4f;
            }
            Ball = new Vector3(at.x, 0, at.y);
            int taker = Nearest(at, -1);
            if (taker >= 0 && Vector2.Distance(players[taker].position, at) < reach)
            {
                owner = taker;
                receiver = -1;
                decision = Rand(.5f, 1.6f);
            }
        }
        void Carry(float dt)
        {
            var o = players[owner];
            var ahead = new Vector2(Mathf.Sin(o.facing * Mathf.Deg2Rad), Mathf.Cos(o.facing * Mathf.Deg2Rad));
            decision -= dt;
            if (o.role == Role.Keeper)
            {
                // Held in the hands, then kicked long up the pitch.
                var hands = o.position + ahead * radius;
                Ball = new Vector3(hands.x, radius * 2.2f, hands.y);
                if (decision <= 0)
                    Clearance(o);
                return;
            }
            var feet = o.position + ahead * reach * .7f;
            Ball = new Vector3(feet.x, Mathf.Abs(Mathf.Sin(o.stride * Mathf.PI)) * radius * .3f, feet.y);
            int rival = Nearest(o.position, 1 - o.team);
            float gap = rival >= 0 ? Vector2.Distance(players[rival].position, o.position) : float.MaxValue;
            // A side with a goal due is hungrier: it wins the ball back sooner, then heads straight for goal.
            float bite = GoalDue(1 - o.team) ? 4 : GoalDue(o.team) ? .25f : 1.1f;
            if (gap < reach * 1.2f && random.NextDouble() < dt * bite)
            {
                Tackles++;
                o.stunned = .8f;
                owner = rival;
                decision = Rand(.4f, 1.2f);
                return;
            }
            float toGoal = halfLength - o.position.x * Dir(o.team);
            if (GoalDue(o.team))
            {
                if (toGoal < halfLength * .62f)
                    Shoot(o);
                return;
            }
            if (decision > 0 && !(gap < reach * 2 && random.NextDouble() < dt * 1.5f))
                return;
            if (toGoal < halfLength * .62f && Mathf.Abs(o.position.y) < halfWidth * .6f && random.NextDouble() < .8)
                Shoot(o);
            else
                Pass(o);
        }

        void Launch(Kick kind, Vector2 to, float speed, float arc)
        {
            kick = kind;
            inFlight = true;
            flightFrom = Ball2;
            flightTo = to;
            flightArc = arc;
            flightTime = 0;
            flightDuration = Mathf.Max(.25f, Vector2.Distance(flightFrom, to) / speed);
            kicker = owner;
            owner = -1;
        }
        void Pass(Player o)
        {
            float dir = Dir(o.team);
            int best = -1;
            float bestScore = float.MinValue;
            for (int j = 0; j < players.Length; j++)
            {
                var q = players[j];
                if (j == owner || q.team != o.team || q.role != Role.Outfield || q.stunned > 0)
                    continue;
                int marker = Nearest(q.position, 1 - o.team);
                float open = marker >= 0 ? Vector2.Distance(q.position, players[marker].position) / halfLength : 1;
                float gain = (q.position.x - o.position.x) * dir / halfLength;
                float length = Vector2.Distance(q.position, o.position) / halfLength;
                float value = gain * 1.2f + Mathf.Min(open, .4f) * 2.5f - Mathf.Abs(length - .45f) * .8f + (float)random.NextDouble() * .7f;
                if (value > bestScore)
                {
                    best = j;
                    bestScore = value;
                }
            }
            if (best < 0)
            {
                decision = .5f;
                return;
            }
            Passes++;
            var mate = players[best];
            float distance = Vector2.Distance(o.position, mate.position);
            var to = OnPitch(mate.position + mate.velocity * (distance / passSpeed) * .8f);
            // Some passes are read by a defender, and now and then one simply goes astray.
            int thief = Nearest(to, 1 - o.team);
            float thiefGap = thief >= 0 ? Vector2.Distance(players[thief].position, to) : float.MaxValue;
            double luck = random.NextDouble();
            float read = GoalDue(1 - o.team) ? .85f : .45f;
            if (thief >= 0 && (thiefGap < reach * 3 && luck < read || luck < .1))
            {
                receiver = thief;
                to = Vector2.Lerp(to, players[thief].position, .6f);
            }
            else
                receiver = best;
            Launch(Kick.Pass, to, passSpeed, distance > halfLength * .6f ? distance * .22f : distance * .03f);
        }
        void Clearance(Player keeper)
        {
            int pick = -1, seen = 0;
            for (int j = 0; j < players.Length; j++)
                if (players[j].team == keeper.team && players[j].role == Role.Outfield && players[j].stunned <= 0 && players[j].home.x > -.4f && random.Next(++seen) == 0)
                    pick = j;
            if (pick < 0)
            {
                decision = .5f;
                return;
            }
            Passes++;
            receiver = pick;
            var to = OnPitch(players[pick].position + new Vector2(Rand(-1, 1), Rand(-1, 1)) * reach);
            Launch(Kick.Pass, to, passSpeed * 1.1f, Vector2.Distance(Ball2, to) * .25f);
        }
        void Shoot(Player o)
        {
            Shots++;
            float dir = Dir(o.team), line = dir * halfLength;
            double luck = random.NextDouble();
            Vector2 to;
            float arc;
            receiver = -1;
            if (GoalDue(o.team))
            {
                shotEnd = ShotEnd.Goal;
                to = new Vector2(line + dir * goalDepth * .7f, Rand(-.8f, .8f) * goalHalf);
                arc = Rand(.1f, .75f) * goalHeight;
            }
            else if (luck < .65)
            {
                shotEnd = ShotEnd.Save;
                to = new Vector2(line - dir * reach * .8f, Rand(-.7f, .7f) * goalHalf);
                arc = Rand(.1f, .5f) * goalHeight;
                receiver = KeeperOf(1 - o.team);
            }
            else
            {
                shotEnd = ShotEnd.Wide;
                to = new Vector2(line + dir * halfLength * .12f, (random.Next(2) * 2 - 1) * goalHalf * Rand(1.3f, 2.2f));
                arc = Rand(.4f, 1.4f) * goalHeight;
            }
            Launch(Kick.Shot, to, shotSpeed, arc);
        }
        void EndShot()
        {
            switch (shotEnd)
            {
                case ShotEnd.Goal:
                    LastScorer = players[kicker].team;
                    scorer = kicker;
                    score[LastScorer]++;
                    Goals++;
                    nextGoal++;
                    Stage = Phase.Goal;
                    PhaseTime = 0;
                    break;
                case ShotEnd.Save:
                    Saves++;
                    owner = receiver;
                    receiver = -1;
                    decision = Rand(.9f, 1.6f);
                    break;
                default:
                    restart = 1.6f;
                    restartKeeper = KeeperOf(1 - players[kicker].team);
                    break;
            }
        }
        Vector2 OnPitch(Vector2 at) => new Vector2(Mathf.Clamp(at.x, -halfLength + reach, halfLength - reach), Mathf.Clamp(at.y, -halfWidth + reach, halfWidth - reach));

        void StepGoal(float dt)
        {
            var hero = players[scorer];
            var corner = new Vector2(Dir(LastScorer) * halfLength * .85f, (hero.position.y >= 0 ? 1 : -1) * halfWidth * .85f);
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p.team == LastScorer && p.role == Role.Outfield)
                {
                    // The scorer runs to the corner flag; team-mates pile in around him, jumping.
                    float angle = i * 2.4f;
                    var target = i == scorer ? corner : hero.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach * 1.6f;
                    Steer(p, target, sprint, dt);
                    bool close = i == scorer || Vector2.Distance(p.position, hero.position) < reach * 3;
                    p.hop = close ? Mathf.Abs(Mathf.Sin(PhaseTime * 9 + i)) * radius * 1.4f : 0;
                }
                else
                    Steer(p, KickoffSpot(i), jog * .6f, dt); // the others trudge back for the restart
            }
            if (PhaseTime > GoalPause)
                BeginKickoff(1 - LastScorer);
        }
        void StepFullTime(float dt)
        {
            int winner = score[0] == score[1] ? -1 : score[0] > score[1] ? 0 : 1;
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                Steer(p, p.position, 0, dt);
                p.hop = p.team == winner && PhaseTime < 3 ? Mathf.Abs(Mathf.Sin(PhaseTime * 8 + i)) * radius : 0;
            }
            if (PhaseTime < FullTimePause)
                return;
            score[0] = score[1] = 0;
            Clock = 0;
            Matches++;
            PlanGoals();
            BeginKickoff(random.Next(2));
        }
    }
}
