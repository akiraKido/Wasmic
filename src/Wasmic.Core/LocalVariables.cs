using System;
using System.Collections.Generic;
using System.Linq;

namespace Wasmic.Core
{
    internal class LocalVariables
    {
        private readonly List<LocalVariableInfo> _localVariableInfos = new List<LocalVariableInfo>();

        internal IReadOnlyDictionary<string, string> LocalVariableMap
        {
            get
            {
                return _localVariableInfos
                    .Where(vi => vi.VariableType == VariableType.LocalVariable)
                    .ToDictionary(vi => vi.Name, vi => vi.Type);
            }
        }

        internal bool Contains(string name) => _localVariableInfos.Any(vi => vi.Name == name);

        internal LocalVariableInfo this[string key] => _localVariableInfos.Single(vi => vi.Name == key);

        internal void AddLocalVariable(string name, string type)
        {
            if(name == null) throw new ArgumentNullException(nameof(name));
            if(type == null) throw new ArgumentNullException(nameof(type));

            _localVariableInfos.Add(new LocalVariableInfo(name, type, VariableType.LocalVariable));
        }

        internal void AddParameter(string name, string type)
        {
            if(name == null) throw new ArgumentNullException(nameof(name));
            if(type == null) throw new ArgumentNullException(nameof(type));

            _localVariableInfos.Add(new LocalVariableInfo(name, type, VariableType.Parameter));
        }

        internal enum VariableType
        {
            None,
            LocalVariable,
            Parameter
        }

        internal class LocalVariableInfo
        {
            public LocalVariableInfo(string name, string type, VariableType variableType)
            {
                if(variableType == VariableType.None) throw new ArgumentException(nameof(variableType));

                Name = name ?? throw new ArgumentNullException(nameof(name));
                Type = type ?? throw new ArgumentNullException(nameof(type));
                VariableType = variableType;
            }

            internal string Name { get; }
            internal string Type { get; }
            internal VariableType VariableType { get; }
        }
    }
}