using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Just ThreeCard Play", menuName = "Stage/Just ThreeCard Play")]
public class JustThreeCardPlay : BossDebuffSO
{
    private const int RequiredCardCount = 3;

    public override void ResetDebuff()
    {
        
    }

    public override void InitDebuff()
    {
        Debug.Log("[Boss] JustThreeCardPlay 발동: 반드시 3장씩 내야 합니다.");
    }

    public override void OnDebuff(List<CardController> selectedCards, ref long chip, ref double multiplier)
    {
        int selectedCount = selectedCards != null ? selectedCards.Count : 0;
        if (selectedCount == RequiredCardCount)
        {
            return;
        }

        chip = 0;
        multiplier = 0;
        Debug.Log("[Boss] 3장이 아닌 카드를 제출했습니다! 페널티 발동.");

        if (!CanSubmitRequiredCardCount())
        {
            Debug.Log("[Boss] 3장을 제출할 수 없는 상태라 패배 처리합니다.");
            CombatManager.instance?.GameOver();
        }
    }

    private static bool CanSubmitRequiredCardCount()
    {
        if (CardManager_2.Instance == null || CardManager_2.Instance.myHandCards == null)
        {
            return true;
        }

        int playableCardCount = 0;
        for (int i = 0; i < CardManager_2.Instance.myHandCards.Count; i++)
        {
            if (CardManager_2.Instance.myHandCards[i] != null)
                playableCardCount++;
        }

        return playableCardCount >= RequiredCardCount;
    }
}
