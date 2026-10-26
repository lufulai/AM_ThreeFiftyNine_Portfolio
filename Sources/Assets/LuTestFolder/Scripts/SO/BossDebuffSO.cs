using System.Collections.Generic;
using UnityEngine;

public abstract class BossDebuffSO : ScriptableObject
{
    [Header("유저 표시 문구")]
    public string debuffName; // 저주 이름

    [TextArea(2, 4)]
    public string debuffDes; // 저주 설명

    [Header("거울 문양")]
    [SerializeField, InspectorName("문양 Sprite")] private Sprite mirrorSymbol;
    [Tooltip("Sprite가 비어 있으면 수묵 시트의 이 번호를 사용합니다. 왼쪽부터 오른쪽, 위에서 아래로 1~12이며, 0이면 거울의 기본 설정을 사용합니다.")]
    [SerializeField, Range(0, 12), InspectorName("수묵 시트 문양 번호")] private int mirrorSymbolSheetNumber;
    [SerializeField, InspectorName("문양 색상")] private Color mirrorSymbolColor = Color.white;

    public Sprite MirrorSymbol => mirrorSymbol;
    public int MirrorSymbolSheetNumber => mirrorSymbolSheetNumber;
    public Color MirrorSymbolColor => mirrorSymbolColor;

    public abstract void OnDebuff(List<CardController> selectedCards, ref long chip, ref double multiplier);
    public virtual void ResetDebuff() { }
    public virtual void InitDebuff() { }

    /// <summary>실제 제출 점수 계산 직전에 한 번 호출됩니다.</summary>
    public virtual void OnBeforeScoreCalculation(List<CardController> selectedCards) { }

    /// <summary>족보에는 포함하되 개별 점수 처리에서 제외할 제출 패인지 반환합니다.</summary>
    public virtual bool ShouldIgnoreSubmittedCardScore(
        CardController card,
        int submittedIndex,
        int submittedCount)
    {
        return false;
    }

    /// <summary>제출 점수 연출까지 끝난 뒤 임시 상태를 복원할 때 호출됩니다.</summary>
    public virtual void OnAfterScorePresentation() { }

    public virtual string GetDisplayDescription()
    {
        return GameContent.Get(this, "debuffDes", debuffDes);
    }
}

//1. 랜덤 월 점수 족보 제외 - CLEAR
//2. 기본 점수와 배수 1/2 - CLEAR
//3. 요구점수 증가 (이 보스는 요구점수 x4배율을 가짐) - CLEAR
//4. 0번의 버리기로 시작 - CLEAR
//5. 플레이한 카드마다 1원씩 잃어버림 (보유한돈이 -가 될 수 있음) -
//6. 손패크기 -1 -
