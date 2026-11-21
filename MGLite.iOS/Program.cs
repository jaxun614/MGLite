using System;
using Foundation;
using UIKit;

namespace MGLite.iOS
{
    [Register("AppDelegate")]
    class Program : UIApplicationDelegate
    {
        private static MGLite.MGLiteGame game;

        internal static void RunGame()
        {
            game = new MGLite.MGLiteGame();
            game.Run();
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            UIApplication.Main(args, null, typeof(Program));
        }

        public override void FinishedLaunching(UIApplication app)
        {
            RunGame();
        }
    }
}
