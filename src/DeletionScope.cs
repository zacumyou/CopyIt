using System;
using System.Collections.Generic;
namespace CopyIt
{
    // KO/EN: 독립 루트와 교차로 소속 요소를 구분 / Distinguish independent roots from junction children.
    internal static class DeletionScope
    {
        internal static bool Allows<T>(T entity,Func<T,bool> selected,Func<T,T> owner,Func<T,bool> orphan,Func<T,bool> touchedJunction) where T:struct
        {
            // KO: 원본 없는 Temp 삭제는 임시 생성물 정리이며 도시의 오브젝트 삭제가 아닙니다.
            // EN: Deleting a Temp with no original only discards transient geometry, not a city entity.
            if(EqualityComparer<T>.Default.Equals(entity,default))return true;
            var seen=new HashSet<T>();var current=entity;
            for(int depth=0;depth<32 && seen.Add(current);depth++)
            {
                if(selected(current)||orphan(current)||(depth>0&&touchedJunction(current)))return true;
                var next=owner(current);
                if(EqualityComparer<T>.Default.Equals(next,default))break;
                current=next;
            }
            return false;
        }
    }
}
