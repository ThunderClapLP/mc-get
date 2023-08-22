using System;

namespace ConsoleTools
{
    public abstract class AnimatableTool
    {
        private bool animationStarted = false;
        public abstract void Update();

        public void StartAnimation()
        {
            if (!CToolsEventLoop.animatableTools.Contains(this))
            {
                animationStarted = true;
                lock (CToolsEventLoop.animatableToolsLock)
                {
                    CToolsEventLoop.animatableTools.Add(this);
                }
                CToolsEventLoop.StartEventLoop(); //start eventloop in case it's not started yet
            }
        }

        public void StopAnimation()
        {
            if (animationStarted) {
                lock (CToolsEventLoop.animatableToolsLock)
                {
                    CToolsEventLoop.animatableTools.Remove(this);
                }
            }
        }
    }
}