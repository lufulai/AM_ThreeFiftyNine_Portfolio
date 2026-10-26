using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Required Score", menuName = "Stage/Required Score")]
public class RequiredScore : BossDebuffSO
{
    private bool isSet = false;

    public override void ResetDebuff()
    {
        isSet = false;
    }

    public override void InitDebuff()
    {
        if (!isSet)
        {
            isSet = true;
            double requiredScoreMultiplier = ArtifactManager.Instance != null
                && ArtifactManager.Instance.HasArtifact("O_Artifact_36")
                ? 2d
                : 4d;
            CombatManager.instance.targetScore = ScoreSafeMath.MultiplyScore(
                CombatManager.instance.targetScore,
                requiredScoreMultiplier,
                1L);

            FieldUIManager.Instance.UpdateScoreUI(CombatManager.instance.targetScore, CombatManager.instance.curRoundScore, 1, 
                JokBoType.None);

            GameLog.Info($"요구 점수 증가 저주 적용({requiredScoreMultiplier:0.#}배): 새로운 목표 점수 {CombatManager.instance.targetScore}");
        }
    }

    public override void OnDebuff(List<CardController> selectedCards, ref long chip, ref double multiplier)
    {
    }
}
