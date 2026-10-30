// Copyright (c) 2026 Cedric Boutin. Licensed under the MIT License. See LICENSE.txt in the project root.

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