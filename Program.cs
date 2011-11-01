using System.Threading;

namespace WerewolvesBot
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            /*ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[] 
			{ 
				new WereWolfInit() 
			};
            ServiceBase.Run(ServicesToRun);*/
            WereWolfInit.Init();
            while (true)
            {
                Thread.Sleep(100);
            }
        }
    }
}
