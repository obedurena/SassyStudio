﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SassyStudio.Editor.Intellisense
{
    class VariableCompletionValue : ICompletionValue
    {
        public SassCompletionValueType Type { get { return SassCompletionValueType.Variable; } }

        public string DisplayText { get; set; }

        public string CompletionText { get; set; }

        public string Description { get; private set; }

        public int Start { get; set; }

        public int End { get; set; }

        public int Length { get; set; }
    }
}
