// Short taps for Haptics.cs (iOS). 0 = light, 1 = medium, anything else = heavy.
#import <UIKit/UIKit.h>

extern "C" void _HapticImpact(int style)
{
    UIImpactFeedbackStyle feedback = style == 0 ? UIImpactFeedbackStyleLight
                                   : style == 1 ? UIImpactFeedbackStyleMedium
                                   : UIImpactFeedbackStyleHeavy;
    UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:feedback];
    [generator impactOccurred];
}
