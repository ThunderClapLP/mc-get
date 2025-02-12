using System;

namespace ConsoleTools
{
    public abstract class AnimatableTool
    {
        private bool animationStarted = false;
        public abstract void Update(bool forceDraw = false);

        public void StartAnimation()
        {
            if (!CToolsEventLoop.animatableTools.Contains(this))
            {
                animationStarted = true;
                lock (CToolsEventLoop.animatableToolsLock)
                {
                    CToolsEventLoop.animatableTools.Add(this);
                }
                Update(true); //make sure to immediately draw the element
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