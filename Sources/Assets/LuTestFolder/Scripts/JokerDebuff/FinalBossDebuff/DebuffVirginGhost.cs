using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Debuff_VirginGhost", menuName = "Boss Debuff/처녀귀신 (기물 판매 전 점수 무효화)")]

public class DebuffVirginGhost : BossDebuffSO
{
    private bool hasSoldArtifact = false;

    public override void InitDebuff() 
    { 
        hasSoldArtifact = false; 
    }

    public void OnArtifactSold() 
    { 
        hasSoldArtifact = true; 
        GameLog.Info("[처녀귀신] 한이 풀려 점수를 정상적으로 획득합니다.");
    }

    public bool IsScoreBlocked() 
    { 
        return !hasSoldArtifact; 
    }

    public override void OnDebuff(List<CardController> selectedCards, ref long chip, ref double multiplier)
    {
        if (!hasSoldArtifact)
        {
            chip = 0;
            multiplier = 0;
        }
    }
}