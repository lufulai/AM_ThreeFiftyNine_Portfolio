using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public sealed class ConsumableAcquireRecord
{
    public ConsumableAcquireRecord(
        AmuletSO amulet,
        int slotIndex,
        string sourceName,
        ArtifactData sourceArtifact = null,
        long presentationSequence = 0)
    {
        Amulet = amulet;
        SlotIndex = slotIndex;
        SourceName = string.IsNullOrWhiteSpace(sourceName) ? GameText.Get("game.ConsumableAcquireRecord.Create.2d00d14fe1", "획득") : sourceName;
        SourceArtifact = sourceArtifact;
        PresentationSequence = presentationSequence;
    }

    public AmuletSO Amulet { get; }
    public int SlotIndex { get; }
    public string SourceName { get; }
    public ArtifactData SourceArtifact { get; }
    public long PresentationSequence { get; }
}

public class ConsumableManager : MonoBehaviour
{
    public static ConsumableManager Instance { get; private set; }

    private const int ConsumableSellPrice = 1;

    [Header("설정")]
    public int maxSlots = 2; // 기본 소모품 슬롯 개수
    public AmuletSO[] inventory;

    [Header("데이터베이스 (랜덤 생성용)")]
    [SerializeField] private List<AmuletSO> allAmuletPool; // 모든 부적 SO 리스트

    public AmuletSO lastUsedAmulet;
    private readonly Dictionary<string, AmuletSO> planetEchoTemplates = new Dictionary<string, AmuletSO>();

    public void RememberUsedPlanet(PlanetSO planet)
    {
        if (planet != null) lastUsedAmulet = GetPlanetEcho(planet);
    }

    private AmuletSO GetPlanetEcho(PlanetSO planet)
    {
        string id = "PlanetEcho:" + planet.cardID;
        if (planetEchoTemplates.TryGetValue(id, out AmuletSO existing)) return existing;
        AmuletSO echo = ScriptableObject.CreateInstance<AmuletSO>();
        echo.cardID = id;
        echo.cardName = planet.cardName;
        echo.amuletType = AmuletType.PlanetReplay;
        echo.amuletSprite = planet.planetSprite;
        echo.amuletDescription = planet.amuletDescription;
        echo.replayPlanet = planet;
        planetEchoTemplates.Add(id, echo);
        return echo;
    }

    public int CurrentConsumableCount => CountOccupiedSlots();
    public int CurrentConsumableCapacity => GetCurrentMaxSlots();

    private readonly List<ConsumableAcquireRecord> pendingAcquireRecords = new List<ConsumableAcquireRecord>();
    private long lastAcquirePresentationSequence;

    public bool HasPendingAcquirePresentation => pendingAcquireRecords.Count > 0;

    public bool IsPendingAcquirePresentation(AmuletSO amulet, int slotIndex)
    {
        if (amulet == null || pendingAcquireRecords.Count == 0)
            return false;

        for (int i = 0; i < pendingAcquireRecords.Count; i++)
        {
            ConsumableAcquireRecord record = pendingAcquireRecords[i];
            if (record == null)
                continue;

            if (ReferenceEquals(record.Amulet, amulet) || record.SlotIndex == slotIndex)
                return true;
        }

        return false;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        RefreshSlotCapacity();
    }

    private void OnDestroy()
    {
        foreach (AmuletSO template in planetEchoTemplates.Values)
            if (template != null) Destroy(template);
        planetEchoTemplates.Clear();
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        LoadFromRunData();
    }

    public bool TryAddConsumable(AmuletSO newAmulet)
    {
        return TryAddConsumable(newAmulet, null, out _, false);
    }

    public bool TryAddConsumable(AmuletSO newAmulet, string acquisitionSourceName)
    {
        return TryAddConsumable(newAmulet, acquisitionSourceName, null, out _, true);
    }

    public bool TryAddConsumable(AmuletSO newAmulet, string acquisitionSourceName, out ConsumableAcquireRecord acquireRecord)
    {
        return TryAddConsumable(newAmulet, acquisitionSourceName, null, out acquireRecord, true);
    }

    public bool TryAddConsumable(AmuletSO newAmulet, string acquisitionSourceName, ArtifactData sourceArtifact)
    {
        return TryAddConsumable(newAmulet, acquisitionSourceName, sourceArtifact, out _, true);
    }

    public bool TryAddConsumable(AmuletSO newAmulet, string acquisitionSourceName, ArtifactData sourceArtifact, out ConsumableAcquireRecord acquireRecord)
    {
        return TryAddConsumable(newAmulet, acquisitionSourceName, sourceArtifact, out acquireRecord, true);
    }

    private bool TryAddConsumable(AmuletSO newAmulet, string acquisitionSourceName, out ConsumableAcquireRecord acquireRecord, bool queuePresentation)
    {
        return TryAddConsumable(newAmulet, acquisitionSourceName, null, out acquireRecord, queuePresentation);
    }

    private bool TryAddConsumable(AmuletSO newAmulet, string acquisitionSourceName, ArtifactData sourceArtifact, out ConsumableAcquireRecord acquireRecord, bool queuePresentation)
    {
        acquireRecord = null;
        if (newAmulet == null) return false;

        RefreshSlotCapacity();
        CompactInventory();

        for (int i = 0; i < inventory.Length; i++)
        {
            if (inventory[i] == null)
            {
                // 원본을 건드리지 않게 복사본을 생성해서 넣습니다.
                AmuletSO addedInstance = Instantiate(newAmulet);
                inventory[i] = addedInstance;
                UnlockManager.RecordItemAcquired(addedInstance);
                // 인벤토리에 실제로 들어간 경우에만 부적 발견을 기록합니다.
                CollectionDiscovery.RecordCard(addedInstance);
                long presentationSequence = queuePresentation && !string.IsNullOrWhiteSpace(acquisitionSourceName)
                    ? ++lastAcquirePresentationSequence
                    : 0;
                acquireRecord = new ConsumableAcquireRecord(
                    addedInstance,
                    i,
                    acquisitionSourceName,
                    sourceArtifact,
                    presentationSequence);
                if (queuePresentation && !string.IsNullOrWhiteSpace(acquisitionSourceName))
                    pendingAcquireRecords.Add(acquireRecord);
                Debug.Log($"<color=cyan>[소모품 획득]</color> {i}번 슬롯에 '{newAmulet.cardName}' 획득!");

                NotifyInventoryChanged();
                return true;
            }
        }

        Debug.LogWarning("⚠️ 소모품 슬롯이 가득 찼습니다!");
        return false;
    }

    public void AddRandomConsumable(int count)
    {
        for(int i = 0; i < count; i++)
        {
            if (!TryAddRandomConsumable(out _)) break;
        }
    }

    public int AddRandomConsumable(int count, string acquisitionSourceName)
    {
        return AddRandomConsumable(count, acquisitionSourceName, null);
    }

    public int AddRandomConsumable(int count, string acquisitionSourceName, ArtifactData sourceArtifact)
    {
        return AddRandomConsumable(count, acquisitionSourceName, sourceArtifact, true);
    }

    public int AddRandomConsumable(
        int count,
        string acquisitionSourceName,
        ArtifactData sourceArtifact,
        bool queuePresentation)
    {
        int addedCount = 0;
        for (int i = 0; i < count; i++)
        {
            if (!TryAddRandomConsumable(
                    acquisitionSourceName,
                    sourceArtifact,
                    queuePresentation,
                    out _,
                    out _))
                break;

            addedCount++;
        }

        return addedCount;
    }

    public bool TryAddRandomConsumable(out AmuletSO addedAmulet)
    {
        return TryAddRandomConsumable(null, out addedAmulet, out _);
    }

    public bool TryAddRandomConsumable(string acquisitionSourceName, out AmuletSO addedAmulet, out ConsumableAcquireRecord acquireRecord)
    {
        return TryAddRandomConsumable(acquisitionSourceName, null, out addedAmulet, out acquireRecord);
    }

    public bool TryAddRandomConsumable(string acquisitionSourceName, ArtifactData sourceArtifact, out AmuletSO addedAmulet, out ConsumableAcquireRecord acquireRecord)
    {
        return TryAddRandomConsumable(
            acquisitionSourceName,
            sourceArtifact,
            !string.IsNullOrWhiteSpace(acquisitionSourceName),
            out addedAmulet,
            out acquireRecord);
    }

    private bool TryAddRandomConsumable(
        string acquisitionSourceName,
        ArtifactData sourceArtifact,
        bool queuePresentation,
        out AmuletSO addedAmulet,
        out ConsumableAcquireRecord acquireRecord)
    {
        addedAmulet = null;
        acquireRecord = null;
        if (allAmuletPool == null || allAmuletPool.Count == 0)
            return false;

        RefreshSlotCapacity();
        if (inventory == null || inventory.Length == 0)
            return false;

        CompactInventory();

        bool hasEmptySlot = false;
        for (int i = 0; i < inventory.Length; i++)
        {
            if (inventory[i] == null)
            {
                hasEmptySlot = true;
                break;
            }
        }

        if (!hasEmptySlot)
            return false;

        AmuletSO randomAmulet = allAmuletPool[Random.Range(0, allAmuletPool.Count)];
        if (!TryAddConsumable(randomAmulet, acquisitionSourceName, sourceArtifact, out acquireRecord, queuePresentation))
            return false;

        addedAmulet = randomAmulet;
        return true;
    }

    public IEnumerator PlayPendingAcquirePresentationRoutine(string sourceName = null)
    {
        List<ConsumableAcquireRecord> records = TakePendingAcquireRecords(sourceName);
        yield return PlayAcquirePresentationRecordsRoutine(records);
    }

    public long CaptureAcquirePresentationSequence()
    {
        return lastAcquirePresentationSequence;
    }

    public IEnumerator PlayPendingAcquirePresentationRoutineAfter(long exclusiveSequence)
    {
        List<ConsumableAcquireRecord> records = TakePendingAcquireRecordsAfter(exclusiveSequence);
        yield return PlayAcquirePresentationRecordsRoutine(records);
    }

    public void ReleasePendingAcquirePresentationAfter(long exclusiveSequence)
    {
        if (TakePendingAcquireRecordsAfter(exclusiveSequence).Count > 0)
            NotifyInventoryChanged();
    }

    private static IEnumerator PlayAcquirePresentationRecordsRoutine(List<ConsumableAcquireRecord> records)
    {
        if (records.Count == 0)
            yield break;

        ConsumableUIManager uiManager = ConsumableUIManager.Instance;
        if (uiManager == null)
            yield break;

        IEnumerator routine = InventoryEffectPresentationCoordinator.PlayConsumableAcquireRecordsRoutine(records);
        try
        {
            while (routine != null && routine.MoveNext())
                yield return routine.Current;
        }
        finally
        {
            (routine as System.IDisposable)?.Dispose();
            uiManager.RefreshUI();
        }
    }

    public bool UseConsumable(int slotIndex)
    {
        if (TryUseConsumable(slotIndex, null))
        {
            SoundManager.PlaySfx(data => data.Consumable_Success);
            return true;
        }

        SoundManager.PlaySfx(data => data.Consumable_Fail);
        return false;
    }

    public bool TryUseConsumable(int slotIndex, AmuletSO expectedAmulet)
    {
        if (TutorialSessionContext.InventoryPracticeActive) return false;
        RefreshSlotCapacity();

        if (slotIndex < 0 || slotIndex >= inventory.Length)
            return false;

        AmuletSO targetAmulet = inventory[slotIndex];
        if (targetAmulet == null)
            return false;
        if (expectedAmulet != null && !ReferenceEquals(targetAmulet, expectedAmulet))
            return false;

        bool releaseSlotBeforeEffect = ConsumableUseRules.RequiresInventorySlotReleasedBeforeEffect(targetAmulet);
        AmuletSO previousLastUsedAmulet = lastUsedAmulet;
        if (releaseSlotBeforeEffect && !RemoveConsumableAt(
                slotIndex,
                rememberAsLastUsed: true,
                GameText.Get("game.ConsumableManager.TryUseConsumable.3955dfaee0", "<color=red>[소모품 제거]</color> {value0}번 슬롯 비움.", new { value0 = slotIndex }),
                saveRunData: false))
        {
            return false;
        }

        bool success = targetAmulet.TryUseAmulet(null);
        if (!success)
        {
            if (releaseSlotBeforeEffect)
                RestoreConsumableAfterFailedUse(targetAmulet, previousLastUsedAmulet);

            return false;
        }

        if (!releaseSlotBeforeEffect)
        {
            DiscardConsumable(slotIndex);
            return true;
        }

        SaveCurrentRunData();
        return true;
    }

    public int GetConsumableSellPrice(AmuletSO amulet)
    {
        if (amulet == null)
            return 0;

        return ConsumableSellPrice;
    }

    public bool SellConsumable(int slotIndex, out int finalPrice)
    {
        if (TutorialSessionContext.InventoryPracticeActive) { finalPrice = 0; return false; }
        finalPrice = 0;
        RefreshSlotCapacity();

        if (slotIndex < 0 || slotIndex >= inventory.Length)
            return false;

        AmuletSO targetAmulet = inventory[slotIndex];
        if (targetAmulet == null)
            return false;

        finalPrice = GetConsumableSellPrice(targetAmulet);
        if (EconomyManager.instance != null && finalPrice > 0)
            EconomyManager.instance.AddMoney(finalPrice);

        SoundManager.PlaySfx(data => data.Shop_Sell);
        string displayName = string.IsNullOrWhiteSpace(targetAmulet.cardName) ? targetAmulet.name : targetAmulet.cardName;
        RemoveConsumableAt(slotIndex, rememberAsLastUsed: false, GameText.Get("game.ConsumableManager.SellConsumable.78e67b7320", "<color=yellow>[소모품 판매]</color> {value0}번 슬롯 '{value1}' 판매 완료! (+{value2}전)", new { value0 = slotIndex, value1 = displayName, value2 = finalPrice }));
        return true;
    }

    public void DiscardConsumable(int slotIndex)
    {
        RemoveConsumableAt(slotIndex, rememberAsLastUsed: true, GameText.Get("game.ConsumableManager.DiscardConsumable.3955dfaee0", "<color=red>[소모품 제거]</color> {value0}번 슬롯 비움.", new { value0 = slotIndex }));
    }

    public bool SwapConsumables(int fromIndex, int toIndex)
    {
        RefreshSlotCapacity();
        CompactInventory();

        int itemCount = CountOccupiedSlots();
        if (fromIndex < 0 || fromIndex >= itemCount) return false;
        if (toIndex < 0 || toIndex >= itemCount) return false;
        if (fromIndex == toIndex) return false;
        if (inventory[fromIndex] == null) return false;

        (inventory[fromIndex], inventory[toIndex]) = (inventory[toIndex], inventory[fromIndex]);
        CompactInventory();
        NotifyInventoryChanged();
        return true;
    }

    public bool MoveConsumable(int fromIndex, int toIndex)
    {
        RefreshSlotCapacity();
        CompactInventory();

        int itemCount = CountOccupiedSlots();
        if (fromIndex < 0 || fromIndex >= itemCount) return false;
        if (toIndex < 0 || toIndex >= itemCount) return false;
        if (fromIndex == toIndex) return false;
        if (inventory[fromIndex] == null) return false;

        AmuletSO movedAmulet = inventory[fromIndex];
        if (fromIndex < toIndex)
        {
            for (int i = fromIndex; i < toIndex; i++)
                inventory[i] = inventory[i + 1];
        }
        else
        {
            for (int i = fromIndex; i > toIndex; i--)
                inventory[i] = inventory[i - 1];
        }

        inventory[toIndex] = movedAmulet;
        CompactInventory();
        NotifyInventoryChanged();
        return true;
    }

    public void RefreshSlotCapacity()
    {
        int targetSlotCount = GetCurrentMaxSlots();
        if (inventory == null)
        {
            inventory = new AmuletSO[targetSlotCount];
            NotifyInventoryChanged();
            return;
        }

        CompactInventory();

        if (inventory.Length == targetSlotCount)
        {
            return;
        }

        AmuletSO[] resizedInventory = new AmuletSO[targetSlotCount];
        int copyCount = Mathf.Min(inventory.Length, resizedInventory.Length);
        for (int i = 0; i < copyCount; i++)
        {
            resizedInventory[i] = inventory[i];
        }

        inventory = resizedInventory;
        CompactInventory();
        NotifyInventoryChanged();
    }

    public void ResetInventoryForNewRun()
    {
        inventory = new AmuletSO[Mathf.Max(0, maxSlots)];
        lastUsedAmulet = null;
        NotifyInventoryChanged();
    }

    public void SyncToRunData()
    {
        if (SaveManager.instance == null)
            return;

        if (SaveManager.instance.currentRunData == null)
            SaveManager.instance.currentRunData = new RunData();

        RunData run = SaveManager.instance.currentRunData;
        run.ownedConsumableIDs ??= new List<string>();
        run.ownedConsumableIDs.Clear();

        // 빈 슬롯은 저장하지 않고 현재 표시 순서대로 고정 ID만 보관합니다.
        if (inventory != null)
        {
            for (int i = 0; i < inventory.Length; i++)
            {
                if (inventory[i] != null && !string.IsNullOrWhiteSpace(inventory[i].cardID))
                    run.ownedConsumableIDs.Add(inventory[i].cardID);
            }
        }

        run.lastUsedAmuletID = lastUsedAmulet != null
            ? lastUsedAmulet.cardID
            : string.Empty;
    }

    public void LoadFromRunData()
    {
        if (SaveManager.instance == null || SaveManager.instance.currentRunData == null)
            return;

        RunData run = SaveManager.instance.currentRunData;
        RestoreInventoryFromCheckpoint(run.ownedConsumableIDs, run.lastUsedAmuletID);
    }

    /// <summary>저장된 부적 ID를 원본 SO로 다시 연결해 인벤토리와 마지막 사용 부적을 복원합니다.</summary>
    public void RestoreInventoryFromCheckpoint(IReadOnlyList<string> consumableIDs, string savedLastUsedAmuletID)
    {
        pendingAcquireRecords.Clear();
        RefreshSlotCapacity();
        inventory = new AmuletSO[Mathf.Max(0, GetCurrentMaxSlots())];

        // 복원 시 획득 연출 없이 슬롯을 직접 채우고 보유 이력만 보완합니다.
        int writeIndex = 0;
        if (consumableIDs != null)
        {
            for (int i = 0; i < consumableIDs.Count && writeIndex < inventory.Length; i++)
            {
                AmuletSO amulet = FindAmuletByID(consumableIDs[i]);
                if (amulet == null)
                {
                    Debug.LogWarning($"[부적 복원] 원본을 찾을 수 없습니다: {consumableIDs[i]}");
                    continue;
                }

                inventory[writeIndex++] = amulet;
                UnlockManager.RecordItemAcquired(amulet);
            }
        }

        lastUsedAmulet = FindAmuletByID(savedLastUsedAmuletID);
        // 천문대도 신장패 echo를 남기므로 구버전의 마지막 echo만으로 획득을 추정하지 않습니다.
        if (lastUsedAmulet != null && lastUsedAmulet.amuletType != AmuletType.PlanetReplay)
            UnlockManager.RecordItemAcquired(lastUsedAmulet);
        if (!string.IsNullOrWhiteSpace(savedLastUsedAmuletID) && lastUsedAmulet == null)
        {
            Debug.LogWarning($"[부적 복원] 마지막 사용 부적을 찾을 수 없습니다: {savedLastUsedAmuletID}");
        }

        NotifyInventoryChanged();
    }

    private int GetCurrentMaxSlots()
    {
        int slotCount = Mathf.Max(0, maxSlots);

        if (VoucherManager.Instance != null)
        {
            slotCount += Mathf.Max(0, VoucherManager.Instance.GetTotalEffectValue(VoucherEffectType.ConsumableSlotBonus));
        }

        return Mathf.Max(0, slotCount);
    }

    private int CountOccupiedSlots()
    {
        if (inventory == null)
            return 0;

        int count = 0;
        for (int i = 0; i < inventory.Length; i++)
        {
            if (inventory[i] != null)
                count++;
        }

        return count;
    }

    private void CompactInventory()
    {
        if (inventory == null || inventory.Length == 0)
            return;

        int writeIndex = 0;
        for (int readIndex = 0; readIndex < inventory.Length; readIndex++)
        {
            AmuletSO amulet = inventory[readIndex];
            if (amulet == null)
                continue;

            if (writeIndex != readIndex)
            {
                inventory[writeIndex] = amulet;
                inventory[readIndex] = null;
            }

            writeIndex++;
        }

        for (int i = writeIndex; i < inventory.Length; i++)
        {
            inventory[i] = null;
        }
    }

    private bool RemoveConsumableAt(int slotIndex, bool rememberAsLastUsed, string logMessage, bool saveRunData = true)
    {
        RefreshSlotCapacity();

        if (slotIndex < 0 || slotIndex >= inventory.Length || inventory[slotIndex] == null)
            return false;

        if (rememberAsLastUsed && inventory[slotIndex].amuletType != AmuletType.Meari)
            lastUsedAmulet = inventory[slotIndex];

        Debug.Log(logMessage);
        inventory[slotIndex] = null;
        CompactInventory();
        NotifyInventoryChanged();

        if (saveRunData)
            SaveCurrentRunData();

        return true;
    }

    private void RestoreConsumableAfterFailedUse(AmuletSO amulet, AmuletSO previousLastUsedAmulet)
    {
        lastUsedAmulet = previousLastUsedAmulet;
        if (amulet == null || inventory == null)
            return;

        for (int i = 0; i < inventory.Length; i++)
        {
            if (inventory[i] != null)
                continue;

            inventory[i] = amulet;
            CompactInventory();
            NotifyInventoryChanged();
            return;
        }

        Debug.LogError("[소모품 사용 복구 실패] 효과가 실패한 소모품을 인벤토리에 되돌릴 빈 슬롯이 없습니다.");
    }

    private static void SaveCurrentRunData()
    {
        if (SaveManager.instance != null && SaveManager.instance.currentRunData != null)
            SaveManager.instance.SaveRunData();
    }

    private AmuletSO FindAmuletByID(string amuletID)
    {
        if (string.IsNullOrWhiteSpace(amuletID))
            return null;

        if (amuletID.StartsWith("PlanetEcho:", System.StringComparison.Ordinal))
        {
            string planetID = amuletID.Substring("PlanetEcho:".Length);
            foreach (PlanetSO planet in Resources.LoadAll<PlanetSO>("Planets"))
                if (planet.cardID == planetID) return GetPlanetEcho(planet);
            return null;
        }

        if (allAmuletPool != null)
        {
            for (int i = 0; i < allAmuletPool.Count; i++)
            {
                AmuletSO amulet = allAmuletPool[i];
                if (amulet != null && amulet.cardID == amuletID)
                    return amulet;
            }
        }

        AmuletSO[] resourceAmulets = Resources.LoadAll<AmuletSO>("Amulets");
        for (int i = 0; i < resourceAmulets.Length; i++)
        {
            AmuletSO amulet = resourceAmulets[i];
            if (amulet != null && amulet.cardID == amuletID)
                return amulet;
        }

        return null;
    }

    private void NotifyInventoryChanged()
    {
        if (ConsumableUIManager.Instance != null)
            ConsumableUIManager.Instance.RefreshUI();
    }

    private List<ConsumableAcquireRecord> TakePendingAcquireRecords(string sourceName)
    {
        List<ConsumableAcquireRecord> records = new List<ConsumableAcquireRecord>();
        if (pendingAcquireRecords.Count == 0)
            return records;

        if (string.IsNullOrWhiteSpace(sourceName))
        {
            records.AddRange(pendingAcquireRecords);
            pendingAcquireRecords.Clear();
            return records;
        }

        for (int i = pendingAcquireRecords.Count - 1; i >= 0; i--)
        {
            ConsumableAcquireRecord record = pendingAcquireRecords[i];
            if (record == null || !string.Equals(record.SourceName, sourceName, System.StringComparison.Ordinal))
                continue;

            records.Insert(0, record);
            pendingAcquireRecords.RemoveAt(i);
        }

        return records;
    }

    private List<ConsumableAcquireRecord> TakePendingAcquireRecordsAfter(long exclusiveSequence)
    {
        List<ConsumableAcquireRecord> records = new List<ConsumableAcquireRecord>();
        for (int i = pendingAcquireRecords.Count - 1; i >= 0; i--)
        {
            ConsumableAcquireRecord record = pendingAcquireRecords[i];
            if (record == null || record.PresentationSequence <= exclusiveSequence)
                continue;

            records.Insert(0, record);
            pendingAcquireRecords.RemoveAt(i);
        }

        return records;
    }
}
