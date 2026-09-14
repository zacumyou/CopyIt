// KO: 원본과 복제 노드의 일대일 대응으로 교차로가 합쳐지거나 갈라지는 오류를 차단합니다.
// EN: A bijection between source and copied nodes guards against merged or split junctions.
using System.Collections.Generic;
namespace CopyIt
{
    // A selected source junction must stay shared, and separate source junctions must not merge.
    public sealed class NodeMapping<T>
    {
        private readonly Dictionary<T,T> forward=new Dictionary<T,T>(),reverse=new Dictionary<T,T>();
        public bool Add(T source,T target){
            if(EqualityComparer<T>.Default.Equals(source,target))return false;
            if(forward.TryGetValue(source,out var mapped)&&!EqualityComparer<T>.Default.Equals(mapped,target))return false;
            if(reverse.TryGetValue(target,out var original)&&!EqualityComparer<T>.Default.Equals(original,source))return false;
            forward[source]=target;reverse[target]=source;return true;
        }
    }
}
