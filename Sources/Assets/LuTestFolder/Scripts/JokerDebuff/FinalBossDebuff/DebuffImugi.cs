using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Debuff_Imugi", menuName = "Boss Debuff/이무기 (점수 1배, 50% 뒤집힘)")]

public class DebuffImugi : BossDebuffSO
{
    public override void OnDebuff(List<CardController> selectedCards, ref long chip, ref double multiplier)
    {
        
    }
}
