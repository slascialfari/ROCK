#import <UIKit/UIKit.h>

// Called from Haptics.cs. style matches UIImpactFeedbackStyle: 0 light, 1 medium, 2 heavy, 3 soft, 4 rigid.
extern "C" void RockHaptics_Impact(int style, float intensity)
{
    static UIImpactFeedbackGenerator *generators[5];
    if (style < 0 || style > 4) style = 1;
    if (generators[style] == nil)
    {
        generators[style] = [[UIImpactFeedbackGenerator alloc] initWithStyle:(UIImpactFeedbackStyle)style];
        [generators[style] prepare];
    }
    [generators[style] impactOccurredWithIntensity:intensity];
    [generators[style] prepare];
}
