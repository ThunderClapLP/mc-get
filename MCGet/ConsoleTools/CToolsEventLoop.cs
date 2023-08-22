using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleTools
{
    public class CToolsEventLoop {
        static bool eventLoopRunning = false;
        public static List<AnimatableTool> animatableTools = new List<AnimatableTool>();
        public static object animatableToolsLock = new object();
        private static Timer timer = new Timer((object? obj) => { EventLoopTick(); }, null, Timeout.Infinite, Timeout.Infinite);

        public static void StartEventLoop()
        {
            if (eventLoopRunning)
                return;
            eventLoopRunning = true;
            timer.Change(0, 100);
        }

        public static void StopEventLoop()
        {
            eventLoopRunning = false;
            timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private static void EventLoopTick()
        {
            lock (animatableToolsLock)
            {
                foreach (AnimatableTool tool in animatableTools)
                {
                    tool.Update();
                }
            }
        }
    }
}