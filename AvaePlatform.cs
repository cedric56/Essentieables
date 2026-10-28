namespace Essentieables;

public class AvaePlatform
{
    public static bool IsMaui
    {
        get
        {
#if WINDOWS || ANDROID || IOS

            return true;
#else
            return false;
#endif
        }

    }
}