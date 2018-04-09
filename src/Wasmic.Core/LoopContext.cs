using System;
using System.Collections.Generic;

namespace Wasmic.Core
{
    internal interface ILoopContext
    {
        void NewContext();
        void EscapeContext();
        void AddNest();
        void EscapeNest();
        int NestCount { get; }
    }

    internal class LoopContext : ILoopContext
    {
        private readonly Stack<LoopContextDetails> _contextStack = new Stack<LoopContextDetails>();
        private LoopContextDetails _currentContext;

        public void NewContext()
        {
            if(_currentContext != null)
            {
                _contextStack.Push(_currentContext);
            }
            _currentContext = new LoopContextDetails();
        }

        public void EscapeContext()
        {
            _currentContext = _contextStack.Count > 0
                ? _contextStack.Pop()
                : null;
        }

        public void AddNest() => _currentContext?.AddNest();
        public void EscapeNest() => _currentContext?.EscapeNest();
        public int NestCount => _currentContext?.NestCount ?? 0;

        private class LoopContextDetails
        {
            internal int NestCount { get; private set; }

            internal void AddNest()
            {
                NestCount++;
            }

            internal void EscapeNest()
            {
                NestCount--;
                if(NestCount < 0) throw new IndexOutOfRangeException("nest count");
            }
        }
    }
}