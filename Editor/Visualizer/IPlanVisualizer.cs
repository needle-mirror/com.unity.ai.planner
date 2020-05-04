using System;
using System.Collections.Generic;

namespace UnityEditor.AI.Planner.Visualizer
{
    interface IPlanVisualizer
    {
        int MaxDepth { get; set; }
        int MaxChildrenNodes { get; set; }
        IVisualizerNode RootNodeOverride { get; set; }
        IList<object> ForcedExpandedNodes { get; }

        void Refresh();
    }
}
