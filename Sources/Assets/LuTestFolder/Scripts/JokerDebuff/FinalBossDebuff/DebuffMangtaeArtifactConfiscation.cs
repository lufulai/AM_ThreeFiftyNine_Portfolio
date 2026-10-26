using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "Debuff_Mangtae_ArtifactConfiscation",
    menuName = "Boss Debuff/망태할아범 (제출마다 기물 임시 회수)")]
public sealed class DebuffMangtaeArtifactConfiscation : BossDebuffSO
{
    /// <summary>점수 계산 직전에 보유 기물 하나를 무작위로 임시 비활성화합니다.</summary>
    public override void OnBeforeScoreCalculation(List<CardController> selectedCards)
    {
        ArtifactManager artifactManager = ArtifactManager.Instance;
        if (artifactManager == null)
        {
            GameLog.Info("[망태할아범][행방불명] 디버프 발동 실패: ArtifactManager가 없습니다.");
            return;
        }

        if (!artifactManager.TryTemporarilySuppressRandomArtifact(out ArtifactData artifact))
        {
            GameLog.Info("[망태할아범][행방불명] 디버프 발동: 가져갈 수 있는 보유 기물이 없습니다.");
            return;
        }

        string artifactName = artifact != null && !string.IsNullOrWhiteSpace(artifact.artifactName)
            ? artifact.artifactName
            : artifact != null ? artifact.name : GameText.Get("game.DebuffMangtaeArtifactConfiscation.OnBeforeScoreCalculation.62ea9807ab", "알 수 없는 기물");
        GameLog.Info($"[망태할아범][행방불명] 디버프 발동: '{artifactName}'의 효과를 이번 제출 점수에서 제외합니다.");
    }

    /// <summary>점수 계산과 연출이 끝난 뒤 가져간 기물을 원래 상태로 돌려줍니다.</summary>
    public override void OnAfterScorePresentation()
    {
        GameLog.Info("[망태할아범][행방불명] 점수 연출 종료: 임시 회수 기물 복원을 요청합니다.");
        ArtifactManager.Instance?.RestoreTemporarilySuppressedArtifact();
    }

    /// <summary>라운드가 강제로 끝나더라도 임시 회수 상태가 남지 않게 복원합니다.</summary>
    public override void ResetDebuff()
    {
        ArtifactManager.Instance?.RestoreTemporarilySuppressedArtifact();
    }

    /// <summary>기물 회수는 제출 생명주기 훅에서 처리하므로 점수 값은 직접 변경하지 않습니다.</summary>
    public override void OnDebuff(List<CardController> selectedCards, ref long chip, ref double multiplier)
    {
        // 실제 회수와 복원은 점수 계산 전·점수 연출 후 수명주기 훅에서 처리합니다.
    }
}

