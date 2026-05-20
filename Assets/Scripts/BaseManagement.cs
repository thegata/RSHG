using System.Collections.Generic;
using UnityEngine;

public class ScoutMission
{
    public int Id;
    public List<Unit> Units = new List<Unit>();
    public float TimeRemaining;
    public float TotalDuration;
    public int WeaponsTaken;
    public int FoodTaken;
    public int MedkitsTaken;
}

public class BaseManagement : MonoBehaviour
{
    public static BaseManagement Instance { get; private set; }

    [Header("Initial Roster")]
    [SerializeField] private int initialUnitCount = 5;
    [SerializeField] private float unitSpacing = 1.8f;
    [Tooltip("Точка, где появятся юниты. Если пусто — заспавнятся в (0,0,0).")]
    [SerializeField] private Transform unitSpawnCenter;

    [Header("Inventory")]
    [SerializeField] private int weapons = 3;
    [SerializeField] private int food = 5;
    [SerializeField] private int medkits = 2;

    [Header("Mission")]
    [SerializeField] private float scoutMissionDuration = 10f;
    [SerializeField] private Vector2 lootFoodRange = new Vector2(0, 3);
    [SerializeField] private Vector2 lootMedkitsRange = new Vector2(0, 2);
    [SerializeField] private float lootMultiplierPerUnit = 1f;

    private List<Unit> units = new List<Unit>();
    private List<ScoutMission> activeMissions = new List<ScoutMission>();
    private int missionIdCounter = 1;

    public IReadOnlyList<Unit> Units => units;
    public IReadOnlyList<ScoutMission> ActiveMissions => activeMissions;

    public int Weapons => weapons;
    public int Food => food;
    public int Medkits => medkits;
    public int TotalUnits => units.Count;
    public float ScoutMissionDuration => scoutMissionDuration;

    public int AvailableUnits
    {
        get
        {
            int c = 0;
            for (int i = 0; i < units.Count; i++) if (units[i].IsAvailable) c++;
            return c;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        SpawnInitialUnits();
    }

    private void Update()
    {
        for (int i = activeMissions.Count - 1; i >= 0; i--)
        {
            var m = activeMissions[i];
            m.TimeRemaining -= Time.deltaTime;
            if (m.TimeRemaining <= 0f)
            {
                CompleteMission(m);
                activeMissions.RemoveAt(i);
            }
        }
    }

    private void SpawnInitialUnits()
    {
        Vector3 center = unitSpawnCenter != null ? unitSpawnCenter.position : Vector3.zero;
        Quaternion rot = unitSpawnCenter != null ? unitSpawnCenter.rotation : Quaternion.identity;

        string[] names = { "Боец Иван", "Боец Колян", "Боец Серёга", "Боец Михалыч", "Боец Витёк",
                           "Боец Палыч", "Боец Лёха", "Боец Жорик", "Боец Андрюха", "Боец Стасян" };

        for (int i = 0; i < initialUnitCount; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Unit_" + (i + 1);
            float x = (i - (initialUnitCount - 1) * 0.5f) * unitSpacing;
            Vector3 localPos = new Vector3(x, 0.5f, 0f);
            Vector3 worldPos = center + rot * localPos;

            var unit = go.AddComponent<Unit>();
            string n = (i < names.Length) ? names[i] : ("Боец " + (i + 1));
            unit.Init(n, worldPos);
            units.Add(unit);
        }
        Debug.Log("[BaseManagement] Заспавнено бойцов: " + units.Count);
    }

    public bool CanStartScoutMission(List<Unit> selected, int wepGive, int foodGive, int medGive, out string reason)
    {
        reason = null;
        if (selected == null || selected.Count == 0) { reason = "Не выбран ни один юнит"; return false; }
        for (int i = 0; i < selected.Count; i++)
            if (!selected[i].IsAvailable) { reason = selected[i].UnitName + " недоступен"; return false; }
        if (wepGive > weapons || foodGive > food || medGive > medkits) { reason = "Не хватает снаряжения"; return false; }
        if (wepGive < 0 || foodGive < 0 || medGive < 0) { reason = "Некорректное количество"; return false; }
        return true;
    }

    public ScoutMission StartScoutMission(List<Unit> selected, int wepGive, int foodGive, int medGive)
    {
        if (!CanStartScoutMission(selected, wepGive, foodGive, medGive, out var why))
        {
            Debug.LogWarning("[BaseManagement] Миссия не запущена: " + why);
            return null;
        }

        weapons -= wepGive;
        food -= foodGive;
        medkits -= medGive;

        for (int i = 0; i < selected.Count; i++) selected[i].GoOnMission();

        var m = new ScoutMission
        {
            Id = missionIdCounter++,
            Units = new List<Unit>(selected),
            TimeRemaining = scoutMissionDuration,
            TotalDuration = scoutMissionDuration,
            WeaponsTaken = wepGive,
            FoodTaken = foodGive,
            MedkitsTaken = medGive,
        };
        activeMissions.Add(m);

        Debug.Log($"[BaseManagement] Разведка #{m.Id} началась. Юнитов: {selected.Count}, оружие: {wepGive}, еда: {foodGive}, аптечки: {medGive}");
        return m;
    }

    private void CompleteMission(ScoutMission m)
    {
        for (int i = 0; i < m.Units.Count; i++)
            if (m.Units[i] != null) m.Units[i].ReturnFromMission();

        weapons += m.WeaponsTaken;
        food += m.FoodTaken;
        medkits += m.MedkitsTaken;

        int unitFactor = Mathf.Max(1, Mathf.RoundToInt(m.Units.Count * lootMultiplierPerUnit));
        int bonusFood = Random.Range((int)lootFoodRange.x, (int)lootFoodRange.y + 1) * unitFactor;
        int bonusMed = Random.Range((int)lootMedkitsRange.x, (int)lootMedkitsRange.y + 1) * unitFactor;
        food += bonusFood;
        medkits += bonusMed;

        Debug.Log($"[BaseManagement] Разведка #{m.Id} вернулась. Принесли: еда +{bonusFood}, аптечки +{bonusMed}.");
    }

    public void AddWeapons(int v) => weapons = Mathf.Max(0, weapons + v);
    public void AddFood(int v) => food = Mathf.Max(0, food + v);
    public void AddMedkits(int v) => medkits = Mathf.Max(0, medkits + v);
}
