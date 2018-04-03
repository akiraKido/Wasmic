using System.Collections.Generic;
using System.Linq;

namespace Wasmic.Core
{
    internal interface IHeap
    {
        (int offset, string name) AllocateOrGetString(string s);
        bool IsAllocated { get; }
        IEnumerable<(string value, int offset)> GetAllocatedStrings();
    }

    internal class LinearHeap : IHeap
    {
        public bool IsAllocated { get; private set; }

        public IEnumerable<(string value, int offset)> GetAllocatedStrings()
            => _strings.Select(kv => (kv.Key, kv.Value.offset));

        private readonly bool[] _heap = new bool[64 * 1024];
        private string _lastName = "a";

        // key = val / value = offset
        private readonly Dictionary<string, (int offset, string name)> _strings
            = new Dictionary<string, (int offset, string name)>();

        public (int offset, string name) AllocateOrGetString(string s)
        {
            if(_strings.ContainsKey(s) == false)
            {
                var offset = Allocate(s.Length);
                var name = GetNextName();
                _strings[s] = (offset, name);
            }

            return _strings[s];
        }


        private string GetNextName()
        {
            if(_lastName.Last() == 'z')
            {
                _lastName = _lastName + "a";
                return _lastName;
            }

            var nextLetter = _lastName.Last() + 1;
            _lastName = _lastName.Substring(0, _lastName.Length - 1) + nextLetter;
            return _lastName;
        }

        private int Allocate(int length)
        {
            if(IsAllocated == false)
            {
                IsAllocated = true;
            }

            int offset = -1;
            for(int i = 0; i < _heap.Length; i++)
            {
                if(_heap[i] == false)
                {
                    offset = i;
                    for(int j = i; j < length; j++)
                    {
                        if(_heap[j] == true) goto CONTINUE;
                    }

                    for(int j = i; j < length; j++)
                    {
                        _heap[j] = true;
                    }
                    goto BREAK;
                }

                CONTINUE:
                continue;
            }
            BREAK:
            return offset;
        }
    }
}