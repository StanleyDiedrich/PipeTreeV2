using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PipeTreeV2
{
    public class ContextModel
    {
        public class CanvasModel
        {
            public IList<string> SystemNames { get; set; }

            public Action<object, PropertyChangedEventArgs> PropertyChanged { get; internal set; }
        }
    }
}