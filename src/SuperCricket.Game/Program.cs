using System;
using System.IO;

string? capturePath = null;
string? captureCamera = null;
if (args.Length > 0)
{
    if (args[0] != "--capture-frame" || args.Length is not (2 or 4) ||
        (args.Length == 4 && args[2] != "--camera"))
    {
        throw new ArgumentException(
            "Usage: SuperCricket.Game [--capture-frame <output.png> [--camera broadcast|behind-striker|bowler-end|square-leg]]");
    }

    capturePath = Path.GetFullPath(args[1]);
    if (args.Length == 4)
        captureCamera = args[3];
}

using var game = new SuperCricket.Game.Game1(capturePath, captureCamera);
game.Run();
