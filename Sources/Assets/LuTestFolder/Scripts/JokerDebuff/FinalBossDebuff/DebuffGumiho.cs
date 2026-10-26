using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Debuff_Gumiho", menuName = "Boss Debuff/구미호 (기물 셔플)")]
public class DebuffGumiho : BossDebuffSO
{
    public override void OnDebuff(List<CardController> selectedCards, ref long chip, ref double multiplier)
    {
       if (ArtifactManager.Instance != null)
        {
            ArtifactManager.Instance.ShuffleArtifacts();
        } 
    }
}
