// KO: 유형이나 표시 이름 대신 프리팹/서브프리팹 ID 쌍을 중복 없이 보관합니다.
// EN: Stores unique prefab/sub-prefab identity pairs, not just categories or display names.
using System;
using System.Collections.Generic;
namespace CopyIt {
    internal sealed class AssetSamples<T> {
        private readonly HashSet<Tuple<T,T>> entries=new HashSet<Tuple<T,T>>();
        internal int Count=>entries.Count;
        internal IEnumerable<Tuple<T,T>> Entries=>entries;
        internal bool Add(T prefab,T subPrefab)=>entries.Add(Tuple.Create(prefab,subPrefab));
        internal bool Contains(T prefab,T subPrefab)=>entries.Contains(Tuple.Create(prefab,subPrefab));
        internal void Clear()=>entries.Clear();
    }
}
