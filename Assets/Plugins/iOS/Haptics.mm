#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator* gLight = nil;
static UIImpactFeedbackGenerator* gMedium = nil;
static UIImpactFeedbackGenerator* gHeavy = nil;
static UISelectionFeedbackGenerator* gSelection = nil;
static UINotificationFeedbackGenerator* gNotification = nil;

extern "C" void _PlayiOS(int type)
{
    @autoreleasepool
    {
        switch (type)
        {
            case 0:
            {
                if (!gSelection) gSelection = [[UISelectionFeedbackGenerator alloc] init];
                [gSelection prepare];
                [gSelection selectionChanged];
                break;
            }
            case 1:
            {
                if (!gLight) gLight = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
                [gLight prepare];
                [gLight impactOccurred];
                break;
            }
            case 2:
            {
                if (!gMedium) gMedium = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
                [gMedium prepare];
                [gMedium impactOccurred];
                break;
            }
            case 3:
            {
                if (!gHeavy) gHeavy = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
                [gHeavy prepare];
                [gHeavy impactOccurred];
                break;
            }
            case 4:
            {
                if (!gNotification) gNotification = [[UINotificationFeedbackGenerator alloc] init];
                [gNotification prepare];
                [gNotification notificationOccurred:UINotificationFeedbackTypeSuccess];
                break;
            }
            case 5:
            {
                if (!gNotification) gNotification = [[UINotificationFeedbackGenerator alloc] init];
                [gNotification prepare];
                [gNotification notificationOccurred:UINotificationFeedbackTypeWarning];
                break;
            }
            case 6:
            {
                if (!gNotification) gNotification = [[UINotificationFeedbackGenerator alloc] init];
                [gNotification prepare];
                [gNotification notificationOccurred:UINotificationFeedbackTypeError];
                break;
            }
            default:
            {
                if (!gLight) gLight = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
                [gLight prepare];
                [gLight impactOccurred];
                break;
            }
        }
    }
}
