# Valley Rail test descriptions

This file translates the automated checks into readable test cases. The complete run passed **17,454 assertions**.

## Train catalog

1. Verify every train model accepts the correct passenger or freight cargo.
2. Verify invalid model and cargo values are rejected.
3. Verify rejected train purchases do not charge money.
4. Verify each model charges its configured purchase price.
5. Verify automatic routing chooses a compatible destination.
6. Verify each model loads up to its configured capacity.
7. Verify train model and loaded cargo survive save/load.
8. Verify every model completes a delivery.
9. Verify every model charges its configured running cost.
10. Verify selling a train refunds half its purchase price.

## Automatic destinations

11. Reject automatic routing when no destination exists.
12. Leave the world unchanged when automatic routing fails.
13. Reject an unknown train ID.
14. Skip stations belonging to the same producer.
15. Skip disconnected compatible stations.
16. Select the shortest reachable compatible station.
17. Reject route changes while a train is loading.
18. Preserve the existing route and cargo after a rejected change.
19. Reject route changes that would discard undelivered cargo.
20. Allow a train starting at a receiving station to find its producer.

## Construction and stations

21. Build straight tracks and charge the correct construction cost.
22. Reject tracks through blocked buildings.
23. Reject deletion of track occupied by an active route.
24. Reject deletion of a bridge occupied by an active route.
25. Reject stations placed on bridges.
26. Reject stations outside an industry or town catchment area.
27. Reject stations without three valid straight track cells.
28. Reject stations placed on curves.
29. Leave money and world state unchanged after rejected station placement.
30. Reject routes that use the same stop twice.
31. Reject routes between two incompatible producers.
32. Reject route changes while loading.
33. Reject unreachable destinations.
34. Protect both legs of a repeating train route from edits.
35. Reject attempts to merge two occupied railway networks.
36. Reject a second train on a single occupied network.
37. Enforce the 1,500-track limit.
38. Enforce the 12-train limit.
39. Confirm construction commands work without event listeners.

## Cargo and economy

40. Deliver coal and increase company funds.
41. Deliver passengers profitably.
42. Deliver goods profitably.
43. Conserve cargo across producer inventory, train cargo and delivered totals.
44. Calculate revenue from units, cargo rate and producer distance rather than track length.
45. Stop active services when company funds reach zero.
46. Recover the expected funds after selling an insolvent train.
47. Keep capital expenses out of the recurring expense ledger.
48. Record running costs and delivery income in the ledger.
49. Keep fleet numbers sequential even when world entity IDs have gaps.

## Industries and factories

50. Replenish raw material industries over time.
51. Keep processing industries empty until input cargo arrives.
52. Reject passenger trains for bulk cargo.
53. Load wood, oil and iron ore at their source industries.
54. Convert delivered input into sawmill, refinery and steel mill output.
55. Pay revenue when processing input is delivered.
56. Prevent one delivery from being counted twice.
57. Allow processed output to be collected.
58. Deliver processed output to a factory or town.
59. Cap processor storage at the configured limit.
60. Reject wrong input cargo.
61. Preserve processor inventory through save/load.
62. Run a complete timber railway from forest to sawmill.
63. Keep the original seven-producer industry layout valid in version 3 saves.
64. Reject raw oil as a town delivery.
65. Keep all three power stations present on a new map.
66. Make power station footprints block track construction.
67. Deliver coal successfully to the new power stations.
68. Preserve the full industry expansion in version 5 saves.

## Crossings and signals

69. Build a diamond crossing with the signal-inclusive price.
70. Allow straight movement on both crossing axes.
71. Reject turns through a diamond crossing.
72. Prevent the pathfinder from turning at a crossing.
73. Start the opposing crossing direction on red.
74. Make a train wait before a red crossing.
75. Resume the train when its direction becomes green.
76. Reproduce signal waits deterministically after save/load.
77. Hold the occupied direction green.
78. Stop conflicting traffic while a crossing is occupied.
79. Protect trailing wagons with the crossing signal state.
80. Deliver coal through a signalled crossing.
81. Protect an active crossing from bulldozing.

## Events

82. Emit no event for rejected station placement.
83. Emit one track-built event with its cost.
84. Store both endpoints in a track-built event.
85. Avoid emitting an event when existing track is laid again.
86. Include station ID and cell in station-built events.
87. Emit no event for a rejected train purchase.
88. Include station and train ID in purchase events.
89. Emit a route-created event without falsely reporting a delivery.
90. Emit departure before arrival and delivery.
91. Include the correct station cells in movement events.
92. Include delivered units and credited revenue in delivery events.
93. Report return-to-station delivery exactly once.
94. Emit station-removal and track-removal events.
95. Keep the newest events when the bounded event queue is full.
96. Empty the event queue correctly after clearing it.

## Expanded map and terrain

97. Make every cell key unique across the 128 × 128 map.
98. Make every cell key reversible back to its original coordinates.
99. Confirm the map contains 16,384 tiles, four times the original area.
100. Accept the coordinate `(127,127)` and reject `(128,0)`.
101. Build tracks beyond the former x=63 boundary without aliasing old cells.
102. Build bridges at the new river rows 78 and 110.
103. Reject tracks through raised mountain terrain.
104. Reject tracks through lake water.
105. Allow a valid railway detour around mountain foothills.
106. Ensure no committed track is placed on a raised tile.
107. Place stations near every new town and industry.
108. Preserve expanded railways and industries through save/load.
109. Preserve the original industry-map layout in version 4 saves.
110. Classify desert, woodland and snowy peak surfaces distinctly.

## Saves and deterministic simulation

111. Round-trip the full world snapshot through the save codec.
112. Reject a tampered map identifier.
113. Reject duplicate entity IDs.
114. Reject invalid track port masks.
115. Reject incomplete bridges.
116. Reject stations moved outside their catchment during load.
117. Reject a second train added to an occupied network during load.
118. Reject disconnected saved routes.
119. Reject saved cargo above train capacity.
120. Accept an untouched valid snapshot.
121. Keep manual and autosave slots independent.
122. Reproduce an exact mid-route replay after save/load.
123. Keep paused simulation ticks unchanged.
124. Preserve expected tick counts at 1×, 2× and 4× speed.
125. Preserve pending ticks during catch-up.
126. Complete a 30-minute delivery soak without losing cargo or state.
127. Keep the measured simulation hot loop allocation at 40 bytes.
128. Validate the maximum supported track and train fixture.

## Towns, roads and tutorial

129. Start towns at 420 residents with the expected buildings and passenger storage.
130. Make served towns grow faster than isolated towns.
131. Increase town level, roads and passenger production after service.
132. Emit a notification when a town level changes.
133. Reproduce city growth exactly after save/load.
134. Keep buildings away from tracks and water and within town bounds.
135. Prevent small villages from starting highways.
136. Build intercity roads one section at a time.
137. Keep rail passenger demand unchanged while a road is unfinished.
138. Complete the road between the two qualifying towns.
139. Preserve road construction progress through save/load.
140. Divert 35% of linked passenger demand after a road completes.
141. Keep full passenger demand for unlinked destinations.
142. Migrate legacy saves to day-zero towns.
143. Reject track through a town building.
144. Charge $500 per building level when demolishing a house.
145. Remove the expected residents after demolition.
146. Allow the cleared cell to accept track.
147. Enforce the town demolition cooldown.
148. Reach towns through the station catchment radius.
149. Start the tutorial at step 1.
150. Fast-forward tutorial steps after completed construction.
151. Complete the tutorial after the first delivery.
152. End the tutorial with no remaining tutorial text.

## Android smoke verification

153. Build the Android development APK successfully.
154. Install the APK on the connected physical Android device.
155. Launch `com.valleyrail.tycoon` successfully.
156. Keep `UnityPlayerGameActivity` in the foreground.
157. Confirm no immediate crash or crash-buffer entry.
158. Render and inspect the expanded terrain map.
159. Pan into the northern mountain and lake region.
160. Observe approximately 29.9 FPS, 0.02 ms average simulation time and 70 MB total memory in the startup sample.

## Light rail to stadiums, beaches and ski resorts

161. List every stadium, beach (open once its car park is finished) and ski resort as a light rail venue.
162. Offer the nearest town railway stations for a line, and refuse freight stations.
163. Plan a line that starts straight on the station forecourt, ends straight beside the venue, and keeps off buildings, hills, water, stations and car parks.
164. Quote an unaffordable line's full price, and charge exactly the quoted price when it is built.
165. Fell the trees on the route and mark the tram cells; refuse a second line to the same venue.
166. Let heavy track cross a line only straight over at right angles, never along it or through a stop.
167. Refuse stations on tram cells, and refuse bulldozing a station or stadium a line serves.
168. Send 25% of the passengers a train unloads at the station to the tram, carrying the rounding over.
169. Book tram fares as income on the line's accounts and charge each tram's running cost a minute.
170. Keep every tram at least the safe gap from the next one, and stop the trams without funds.
171. Never let a tram enter a railway crossing while a train is near, and still cross between trains.
172. Save and load lines exactly; load older saves with no lines; reject corrupt lines; idle a line whose station is gone.
173. Replay light rail exactly after a save, and refund half when a line is removed.
174. Keep town buildings off tram cells while towns grow for 20,000 ticks.
175. Build a line from the BUILD tray's LIGHT RAIL panel and run a three-section tram on it with no physics components (PlayMode).

## Reproduction command

```sh
dotnet run --project Tests/CoreChecks.csproj
```

The executable assertions are implemented in [Tests/Program.cs](/Users/michael/Downloads/transport/Tests/Program.cs). The broader evidence and device notes are in [TEST_REPORT.md](/Users/michael/Downloads/transport/TEST_REPORT.md) and [VALIDATION.md](/Users/michael/Downloads/transport/VALIDATION.md).
