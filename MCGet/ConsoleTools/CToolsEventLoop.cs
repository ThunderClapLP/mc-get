using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConsoleTools
{
    public class CToolsEventLoop {
        static bool eventLoopRunning = false;
        public static List<AnimatableTool> animatableTools = new List<AnimatableTool>();

        public static void StartEventLoop()
        {
            if (eventLoopRunning)
                return;
            eventLoopRunning = true;
            _ = EventLoopAsync();
        }

        public static void StopEventLoop()
        {
            eventLoopRunning = false;
        }

        private static async Task EventLoopAsync()
        {
            while (eventLoopRunning) {
                await Task.Delay(100);
                foreach (AnimatableTool tool in animatableTools) {
                    tool.Update(); //not thread safe, but should do for the moment
                }
            }
        }
    }
}