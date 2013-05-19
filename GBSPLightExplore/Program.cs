using System;

namespace GBSPLightExplore
{
#if WINDOWS || XBOX
    static class Program
    {
		[STAThread]
        static void Main(string[] args)
        {
//			System.Windows.Forms.Application.EnableVisualStyles();
//			System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            using (Explore game = new Explore())
            {
                game.Run();
				//do settings save here if wanted
            }
        }
    }
#endif
}

