// Checks ValleyTrackpad's summing without a trackpad: compiled with the plugin source into a command-line test by
// Tools/build_trackpad_plugin.sh, which refuses to build the bundle if any check fails.
#import <AppKit/AppKit.h>
#include <stdio.h>

typedef struct { float magnify, rotate, panX, panY, wheel; int rotateEnded, precise; } VRTrackpadFrame;
void VRTrackpadRecord(int kind, float a, float b, int flag);
int VRTrackpadRead(VRTrackpadFrame *frame);

static int failures;
static void Check(int ok, const char *what)
{
    if (!ok)
    {
        failures++;
        fprintf(stderr, "FAIL: %s\n", what);
    }
}

int main(void)
{
    @autoreleasepool
    {
        [NSApplication sharedApplication];
        VRTrackpadFrame f;
        Check(VRTrackpadRead(&f) == 1, "the event monitor installs");
        Check(f.magnify == 0 && f.rotate == 0 && f.panX == 0 && f.panY == 0 && f.wheel == 0 && !f.rotateEnded && !f.precise, "a first read is empty");

        VRTrackpadRecord(1, .25f, 0, 0);
        VRTrackpadRecord(1, -.05f, 0, 0);
        VRTrackpadRecord(2, 12, 0, 0);
        VRTrackpadRecord(2, 8, 0, 1);
        VRTrackpadRecord(3, 10, -4, 1);
        VRTrackpadRecord(3, 6, 2, 1);
        VRTrackpadRecord(3, 0, 3, 0);
        VRTrackpadRead(&f);
        Check(f.magnify > .199f && f.magnify < .201f, "pinch magnification sums");
        Check(f.rotate == 20, "twist degrees sum");
        Check(f.rotateEnded == 1, "a rotate gesture's end is reported");
        Check(f.panX == 16 && f.panY == -2, "precise scroll sums as a pan");
        Check(f.precise == 1, "precise scroll is flagged");
        Check(f.wheel == 3, "a line-based wheel stays a wheel, not a pan");

        VRTrackpadRead(&f);
        Check(f.magnify == 0 && f.rotate == 0 && f.panX == 0 && f.wheel == 0 && !f.rotateEnded && !f.precise, "reading clears the sums");
        Check(VRTrackpadRead(NULL) == 1, "a read without a frame only clears");
    }
    if (failures)
        return 1;
    printf("ValleyTrackpad: all checks passed\n");
    return 0;
}
