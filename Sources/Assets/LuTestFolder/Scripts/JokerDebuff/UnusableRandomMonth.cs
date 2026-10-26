using System.Collections.Generic;
using UnityEngine;

// 랜덤한 월[1~10] 무력화 (족보 제외, 점수 제외) 대충 이런식 
[CreateAssetMenu(fileName = "UnusableRandomMonth", menuName = "Stage/UnusableRandomMonth")]
public class UnusableRandomMonth : BossDebuffSO
{
    public _CardMonth UnusableMonth;
    private bool isSet = false;
    public bool HasSelectedMonth => isSet;

    public void SetSelectedMonth(_CardMonth month)
    {
        int monthIndex = Mathf.Clamp((int)month, 0, 9);
        UnusableMonth = (_CardMonth)monthIndex;
        isSet = true;
    }

    public override void ResetDebuff()
    {
        isSet = false;
    }
    // 플레이 하기 전 저주 적용
    public override void InitDebuff()
    {
        if (!isSet)
        {
            SelectRandomMonth();
            int monthNumber = (int)UnusableMonth + 1;
            GameLog.Info($"보스 저주 설정됨: {monthNumber}월 패는 무효화");
        }
    }

    public override string GetDisplayDescription()
    {
        if (!isSet)
            SelectRandomMonth();

        return GameText.Get("game.UnusableRandomMonth.GetDisplayDescription.56ed879a5a", "{value0}월 패는 점수와 족보 계산에서 제외됩니다.", new { value0 = (int)UnusableMonth + 1 });
    }

    // 저주의 효과를
    public override void OnDebuff(List<CardController> selectedCards, ref long chip, ref double multiplier)
    {
        for (int i = selectedCards.Count - 1; i >= 0; i--)
        {
            // BaseCardSO가 일반 화투패(CardSO)일 때만 월과 점수를 확인합니다.
            if (selectedCards[i].cardData is CardSO card && card.cardMonth == UnusableMonth)
            {
                chip -= card.cardScore;

                selectedCards.RemoveAt(i);
                
                GameLog.Info($"{(int)UnusableMonth + 1}월 패 감지: 점수 차감 및 족보 계산에서 제외 처리됨.");
            }
        }
    }

    private void SelectRandomMonth()
    {
        UnusableMonth = (_CardMonth)Random.Range(0, 10);
        isSet = true;
    }
}
