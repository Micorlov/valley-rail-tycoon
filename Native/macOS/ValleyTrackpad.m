// ValleyTrackpad: macOS trackpad gestures for Valley Rail's camera.
//
// Unity's Input System reports a two-finger trackpad swipe as a mouse wheel and never sees pinch or rotate, so this
// plugin watches the app's own event stream with a local monitor and sums what arrived since the previous frame.
// The game reads (and clears) the sums once per frame through VRTrackpadRead. Built into
// Assets/Plugins/macOS/ValleyTrackpad.bundle by Tools/build_trackpad_plugin.sh.
#import <AppKit/AppKit.h>
#include <string.h>

// Mirrors ValleyRail.TrackpadFrame (sequential layout: five floats, then two ints).
typedef struct
{
    float magnify;    // summed pinch magnification; positive when the fingers spread
    float rotate;     // summed twist in degrees; positive anticlockwise
    float panX, panY; // summed precise (trackpad / Magic Mouse) scroll, in pixels, as AppKit reports it
    float wheel;      // summed line-based mouse-wheel scroll
    int rotateEnded;  // a rotate gesture ended or was cancelled
    int precise;      // any precise scroll arrived
} VRTrackpadFrame;

enum { VRMagnify = 1, VRRotate = 2, VRScroll = 3 };

static VRTrackpadFrame pending;
static id monitor;

// Adds one event to the pending sums. Magnify: a = magnification. Rotate: a = degrees, flag = the gesture ended.
// Scroll: a, b = x and y deltas, flag = precise (pixels) rather than wheel lines.
void VRTrackpadRecord(int kind, float a, float b, int flag)
{
    switch (kind)
    {
        case VRMagnify:
            pending.magnify += a;
            break;
        case VRRotate:
            pending.rotate += a;
            if (flag)
                pending.rotateEnded = 1;
            break;
        case VRScroll:
            if (flag)
            {
                pending.panX += a;
                pending.panY += b;
                pending.precise = 1;
            }
            else
                pending.wheel += b;
            break;
        default:
            break;
    }
}

static NSEvent *Observe(NSEvent *event)
{
    switch (event.type)
    {
        case NSEventTypeMagnify:
            VRTrackpadRecord(VRMagnify, (float)event.magnification, 0, 0);
            break;
        case NSEventTypeRotate:
            VRTrackpadRecord(VRRotate, event.rotation, 0, (event.phase & (NSEventPhaseEnded | NSEventPhaseCancelled)) != 0);
            break;
        case NSEventTypeScrollWheel:
            if (event.hasPreciseScrollingDeltas)
            {
                // Points to the backing pixels Unity's screen coordinates use (2 on Retina).
                CGFloat scale = event.window ? event.window.backingScaleFactor : NSScreen.mainScreen.backingScaleFactor;
                VRTrackpadRecord(VRScroll, (float)(event.scrollingDeltaX * scale), (float)(event.scrollingDeltaY * scale), 1);
            }
            else
                VRTrackpadRecord(VRScroll, (float)event.scrollingDeltaX, (float)event.scrollingDeltaY, 0);
            break;
        default:
            break;
    }
    return event;
}

// Installs the monitor on the first call (Unity calls from the main thread), then copies out and clears everything
// summed since the previous call. Returns 1 while the monitor is installed, 0 if AppKit refused it.
int VRTrackpadRead(VRTrackpadFrame *frame)
{
    if (!monitor)
        monitor = [NSEvent addLocalMonitorForEventsMatchingMask:NSEventMaskMagnify | NSEventMaskRotate | NSEventMaskScrollWheel
                                                         handler:^NSEvent *(NSEvent *event) { return Observe(event); }];
    if (frame)
        *frame = pending;
    memset(&pending, 0, sizeof pending);
    return monitor != nil;
}
