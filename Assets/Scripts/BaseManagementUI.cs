using System.Collections.Generic;
using UnityEngine;

public class BaseManagementUI : MonoBehaviour
{
    public enum Tab { Yard, Scout, Stash, Attack }

    private Tab activeTab = Tab.Yard;
    private HashSet<Unit> selectedForScout = new HashSet<Unit>();
    private int scoutWeapons, scoutFood, scoutMedkits;

    private static readonly Color DARK_BG = new Color(0.09f, 0.08f, 0.06f, 0.98f);
    private static readonly Color WOOD = new Color(0.22f, 0.14f, 0.09f, 1f);
    private static readonly Color WOOD_LIGHT = new Color(0.36f, 0.24f, 0.15f, 1f);
    private static readonly Color PARCHMENT = new Color(0.85f, 0.76f, 0.58f, 1f);
    private static readonly Color OLIVE = new Color(0.24f, 0.27f, 0.16f, 1f);
    private static readonly Color OLIVE_DARK = new Color(0.16f, 0.18f, 0.10f, 1f);
    private static readonly Color CONCRETE = new Color(0.28f, 0.28f, 0.25f, 1f);
    private static readonly Color RUST = new Color(0.56f, 0.18f, 0.16f, 1f);
    private static readonly Color RUST_BRIGHT = new Color(0.78f, 0.27f, 0.22f, 1f);
    private static readonly Color GOLD = new Color(0.80f, 0.62f, 0.30f, 1f);
    private static readonly Color GOLD_BRIGHT = new Color(0.95f, 0.78f, 0.40f, 1f);
    private static readonly Color CREAM = new Color(0.96f, 0.88f, 0.72f, 1f);
    private static readonly Color INK = new Color(0.16f, 0.12f, 0.08f, 1f);
    private static readonly Color INK_FADED = new Color(0.40f, 0.32f, 0.22f, 1f);
    private static readonly Color SUCCESS_GREEN = new Color(0.38f, 0.55f, 0.25f, 1f);

    private Texture2D texWood, texParchment, texOlive, texConcrete, texDark, texDarker;
    private Texture2D texGold, texRust, texRustBright, texCardIdle, texCardSel, texCardLocked;

    private GUIStyle bigTitle, subTitle, headerInk, headerCream, body, bodyCream, small, smallCream, mono;
    private GUIStyle tabBtn, tabBtnActive, btn, btnDanger, btnAction, btnAccept;

    private const float TopH = 78f;
    private const float TabsW = 240f;
    private const float BottomH = 180f;
    private const float Pad = 14f;

    private bool initialized;

    private static Texture2D MakeSolid(Color c)
    {
        var t = new Texture2D(2, 2);
        var px = new Color[4]; for (int i = 0; i < 4; i++) px[i] = c;
        t.SetPixels(px); t.Apply();
        return t;
    }

    private static Texture2D MakeNoisyTex(Color baseColor, float amount, int size = 96, float scale = 0.08f)
    {
        var t = new Texture2D(size, size);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float n = (Mathf.PerlinNoise(x * scale, y * scale) - 0.5f) * amount;
            float g = (Random.value - 0.5f) * amount * 0.5f;
            var c = baseColor;
            c.r = Mathf.Clamp01(c.r + n + g);
            c.g = Mathf.Clamp01(c.g + n * 0.95f + g);
            c.b = Mathf.Clamp01(c.b + n * 0.85f + g);
            t.SetPixel(x, y, c);
        }
        t.Apply(); t.wrapMode = TextureWrapMode.Repeat;
        return t;
    }

    private static Texture2D MakeWoodTex()
    {
        int size = 256;
        var t = new Texture2D(size, size);
        for (int y = 0; y < size; y++)
        {
            float band = Mathf.PerlinNoise(y * 0.04f, 0.0f);
            for (int x = 0; x < size; x++)
            {
                float grain = Mathf.PerlinNoise(x * 0.015f, y * 0.6f);
                float dark = band * 0.35f + grain * 0.25f;
                var c = Color.Lerp(WOOD, WOOD_LIGHT, dark);
                t.SetPixel(x, y, c);
            }
        }
        t.Apply(); t.wrapMode = TextureWrapMode.Repeat;
        return t;
    }

    private void Init()
    {
        texWood = MakeWoodTex();
        texParchment = MakeNoisyTex(PARCHMENT, 0.18f);
        texOlive = MakeNoisyTex(OLIVE, 0.08f);
        texConcrete = MakeNoisyTex(CONCRETE, 0.10f);
        texDark = MakeSolid(DARK_BG);
        texDarker = MakeSolid(new Color(0.06f, 0.05f, 0.04f, 0.98f));
        texGold = MakeSolid(GOLD);
        texRust = MakeSolid(RUST);
        texRustBright = MakeSolid(RUST_BRIGHT);
        texCardIdle = MakeSolid(new Color(0.62f, 0.54f, 0.36f, 1f));
        texCardSel = MakeSolid(new Color(0.88f, 0.72f, 0.32f, 1f));
        texCardLocked = MakeSolid(new Color(0.40f, 0.36f, 0.28f, 1f));

        bigTitle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        bigTitle.normal.textColor = GOLD_BRIGHT;

        subTitle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleLeft };
        subTitle.normal.textColor = new Color(CREAM.r, CREAM.g, CREAM.b, 0.7f);

        headerInk = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        headerInk.normal.textColor = RUST;

        headerCream = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        headerCream.normal.textColor = GOLD_BRIGHT;

        body = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft, wordWrap = true };
        body.normal.textColor = INK;

        bodyCream = new GUIStyle(body); bodyCream.normal.textColor = CREAM;

        small = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Italic, wordWrap = true };
        small.normal.textColor = INK_FADED;

        smallCream = new GUIStyle(small); smallCream.normal.textColor = new Color(CREAM.r, CREAM.g, CREAM.b, 0.75f);

        mono = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        mono.normal.textColor = CREAM;

        tabBtn = new GUIStyle(GUI.skin.button) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(16, 10, 14, 14) };
        tabBtn.normal.background = MakeSolid(WOOD_LIGHT);
        tabBtn.hover.background = MakeSolid(new Color(0.46f, 0.32f, 0.20f));
        tabBtn.normal.textColor = CREAM;
        tabBtn.hover.textColor = CREAM;

        tabBtnActive = new GUIStyle(tabBtn);
        tabBtnActive.normal.background = MakeSolid(RUST_BRIGHT);
        tabBtnActive.hover.background = MakeSolid(new Color(0.86f, 0.32f, 0.26f));
        tabBtnActive.normal.textColor = GOLD_BRIGHT;
        tabBtnActive.hover.textColor = GOLD_BRIGHT;

        btn = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(10, 10, 6, 6) };
        btn.normal.background = MakeSolid(WOOD_LIGHT);
        btn.hover.background = MakeSolid(new Color(0.46f, 0.32f, 0.20f));
        btn.normal.textColor = CREAM;
        btn.hover.textColor = CREAM;

        btnAction = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(20, 20, 14, 14) };
        btnAction.normal.background = MakeSolid(RUST);
        btnAction.hover.background = MakeSolid(RUST_BRIGHT);
        btnAction.normal.textColor = GOLD_BRIGHT;
        btnAction.hover.textColor = CREAM;

        btnAccept = new GUIStyle(btnAction);
        btnAccept.normal.background = MakeSolid(SUCCESS_GREEN);
        btnAccept.hover.background = MakeSolid(new Color(0.50f, 0.68f, 0.32f));

        btnDanger = new GUIStyle(btn);
        btnDanger.normal.background = MakeSolid(RUST);
        btnDanger.hover.background = MakeSolid(RUST_BRIGHT);

        initialized = true;
    }

    private void DrawBorder(Rect r, Color color, int thickness = 2)
    {
        var t = MakeSolid(color);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), t);
        GUI.DrawTexture(new Rect(r.x, r.yMax - thickness, r.width, thickness), t);
        GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), t);
        GUI.DrawTexture(new Rect(r.xMax - thickness, r.y, thickness, r.height), t);
    }

    private void OnGUI()
    {
        if (!initialized) Init();
        var bm = BaseManagement.Instance;
        if (bm == null)
        {
            GUI.Label(new Rect(20, 20, 700, 30), "BaseManagement не найден в сцене.", bodyCream);
            return;
        }

        DrawTopBar(bm);
        DrawTabsBar();

        Rect content = new Rect(TabsW, TopH, Screen.width - TabsW, Screen.height - TopH - BottomH);
        GUI.DrawTexture(content, texParchment);
        DrawBorder(content, INK, 2);
        DrawBorder(new Rect(content.x + 6, content.y + 6, content.width - 12, content.height - 12), new Color(GOLD.r, GOLD.g, GOLD.b, 0.7f), 1);

        switch (activeTab)
        {
            case Tab.Yard: DrawYardTab(bm, content); break;
            case Tab.Scout: DrawScoutTab(bm, content); break;
            case Tab.Stash: DrawStashTab(bm, content); break;
            case Tab.Attack: DrawAttackTab(content); break;
        }

        DrawBottomBar(bm);
    }

    private void DrawTopBar(BaseManagement bm)
    {
        var r = new Rect(0, 0, Screen.width, TopH);
        GUI.DrawTexture(r, texWood);
        GUI.DrawTexture(new Rect(0, TopH - 4, Screen.width, 2), texGold);
        GUI.DrawTexture(new Rect(0, TopH - 1, Screen.width, 1), MakeSolid(new Color(0, 0, 0, 0.5f)));

        GUI.Label(new Rect(Pad + 4, 6, 800, 36), "★ ШТАБ «БАЗА № 1»", bigTitle);
        GUI.Label(new Rect(Pad + 10, 44, 800, 22), "ОПЕРАТИВНАЯ ЕДИНИЦА — РАЙОН СОВЕТСКИЙ", subTitle);

        float xRight = Screen.width - Pad;
        DrawResource(ref xRight, "АПТЕЧКИ", bm.Medkits.ToString(), new Color(1f, 0.55f, 0.50f));
        DrawResource(ref xRight, "ПРОВИАНТ", bm.Food.ToString(), new Color(0.75f, 1f, 0.6f));
        DrawResource(ref xRight, "АРСЕНАЛ", bm.Weapons.ToString(), GOLD_BRIGHT);
        DrawResource(ref xRight, "ЛИЧ.СОСТАВ", bm.AvailableUnits + " / " + bm.TotalUnits, new Color(0.75f, 0.88f, 1f));
    }

    private void DrawResource(ref float xRight, string name, string value, Color valColor)
    {
        float w = 110f;
        var nameStyle = new GUIStyle(small) { alignment = TextAnchor.MiddleRight };
        nameStyle.normal.textColor = new Color(CREAM.r, CREAM.g, CREAM.b, 0.75f);
        var valStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        valStyle.normal.textColor = valColor;

        var box = new Rect(xRight - w, 8, w, TopH - 16);
        GUI.Label(new Rect(box.x, box.y, box.width, 16), name, nameStyle);
        GUI.Label(new Rect(box.x, box.y + 18, box.width, 30), value, valStyle);
        xRight -= w + 6f;
    }

    private void DrawTabsBar()
    {
        var r = new Rect(0, TopH, TabsW, Screen.height - TopH);
        GUI.DrawTexture(r, texOlive);
        DrawBorder(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), new Color(GOLD.r, GOLD.g, GOLD.b, 0.4f), 1);

        var label = new GUIStyle(headerCream) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
        GUI.Label(new Rect(0, TopH + 10, TabsW, 26), "О П Е Р А Ц И И", label);
        GUI.DrawTexture(new Rect(Pad + 20, TopH + 38, TabsW - 2 * (Pad + 20), 2), texGold);

        float y = TopH + 60;
        if (DrawTab("ДВОР", Tab.Yard, y)) activeTab = Tab.Yard; y += 60;
        if (DrawTab("ВЫЛАЗКА", Tab.Scout, y)) activeTab = Tab.Scout; y += 60;
        if (DrawTab("СКЛАД", Tab.Stash, y)) activeTab = Tab.Stash; y += 60;
        if (DrawTab("АТАКА", Tab.Attack, y)) activeTab = Tab.Attack; y += 60;

        var motto = new GUIStyle(smallCream) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        GUI.Label(new Rect(Pad, Screen.height - BottomH - 70, TabsW - 2 * Pad, 40),
            "«Береги патроны —\nпатрон бережёт пацана.»", motto);
    }

    private bool DrawTab(string label, Tab tab, float y)
    {
        var style = activeTab == tab ? tabBtnActive : tabBtn;
        bool clicked = GUI.Button(new Rect(Pad, y, TabsW - 2 * Pad, 48), "   " + label, style);
        if (activeTab == tab)
            GUI.DrawTexture(new Rect(Pad, y, 5, 48), texGold);
        return clicked;
    }

    private void DrawYardTab(BaseManagement bm, Rect content)
    {
        float x = content.x + Pad + 10;
        float y = content.y + Pad + 10;
        GUI.Label(new Rect(x, y, 600, 30), "ЛИЧНЫЙ СОСТАВ", headerInk);
        GUI.DrawTexture(new Rect(x, y + 32, 220, 2), MakeSolid(RUST));
        y += 44;
        GUI.Label(new Rect(x, y, 700, 20), "Состав отряда. Серым отмечены те, кто на задании.", small);
        y += 30;

        float cardW = 200f, cardH = 110f, gap = 12f;
        int perRow = Mathf.Max(1, Mathf.FloorToInt((content.width - 2 * Pad - 20) / (cardW + gap)));
        int idx = 0;
        foreach (var u in bm.Units)
        {
            if (u == null) continue;
            int col = idx % perRow, row = idx / perRow;
            var cr = new Rect(x + col * (cardW + gap), y + row * (cardH + gap), cardW, cardH);

            Texture2D bg = !u.IsAvailable ? texCardLocked : (u.IsSelected ? texCardSel : texCardIdle);
            GUI.DrawTexture(cr, bg);
            DrawBorder(cr, INK, 2);

            var swatch = new Rect(cr.x + 10, cr.y + 10, 28, 28);
            GUI.DrawTexture(swatch, MakeSolid(INK));
            GUI.DrawTexture(new Rect(swatch.x + 2, swatch.y + 2, swatch.width - 4, swatch.height - 4),
                MakeSolid(GuessUnitColor(u)));

            GUI.Label(new Rect(cr.x + 48, cr.y + 8, cr.width - 56, 22), u.UnitName, headerCream);
            string status = u.IsAvailable ? "В РАСПОРЯЖЕНИИ" : "НА ЗАДАНИИ";
            var statusStyle = new GUIStyle(body) { fontStyle = FontStyle.Bold, fontSize = 13 };
            statusStyle.normal.textColor = u.IsAvailable ? new Color(0.20f, 0.45f, 0.15f) : RUST;
            GUI.Label(new Rect(cr.x + 48, cr.y + 32, cr.width - 56, 20), status, statusStyle);

            GUI.Label(new Rect(cr.x + 12, cr.y + 60, cr.width - 24, 18), "район: Советский", small);
            GUI.Label(new Rect(cr.x + 12, cr.y + 78, cr.width - 24, 18), u.IsAvailable ? "готов к выходу" : "ожидайте...", small);

            idx++;
        }
    }

    private Color GuessUnitColor(Unit u)
    {
        var rend = u.GetComponent<Renderer>();
        if (rend != null && rend.sharedMaterial != null) return rend.sharedMaterial.color;
        return Color.gray;
    }

    private void DrawScoutTab(BaseManagement bm, Rect content)
    {
        float x = content.x + Pad + 10;
        float y = content.y + Pad + 10;
        float w = content.width - 2 * (Pad + 10);

        GUI.Label(new Rect(x, y, 600, 30), "ВЫЛАЗКА В ГОРОД", headerInk);
        GUI.DrawTexture(new Rect(x, y + 32, 220, 2), MakeSolid(RUST));
        y += 44;
        GUI.Label(new Rect(x, y, w, 20),
            $"Длительность: {bm.ScoutMissionDuration:0} сек. Кого выслать и что им дать?", small);
        y += 30;

        float colW = (w - Pad) * 0.5f;
        float listH = content.height - (y - content.y) - 120f;

        GUI.Label(new Rect(x, y, colW, 22), "БОЙЦЫ", headerInk);
        GUI.Label(new Rect(x + colW + Pad, y, colW, 22), "СНАРЯЖЕНИЕ", headerInk);
        y += 28;

        var listRect = new Rect(x, y, colW, listH);
        var gearRect = new Rect(x + colW + Pad, y, colW, listH);
        GUI.DrawTexture(listRect, MakeSolid(new Color(0.95f, 0.86f, 0.65f)));
        DrawBorder(listRect, INK, 2);
        GUI.DrawTexture(gearRect, MakeSolid(new Color(0.95f, 0.86f, 0.65f)));
        DrawBorder(gearRect, INK, 2);

        DrawScoutUnits(bm, listRect);
        DrawScoutGear(bm, gearRect);

        float btnY = y + listH + 14;
        DrawSendButton(bm, x, btnY, w);
    }

    private void DrawScoutUnits(BaseManagement bm, Rect rect)
    {
        float itemH = 44f;
        float yy = rect.y + 8;
        var nameStyle = new GUIStyle(body) { fontStyle = FontStyle.Bold };
        foreach (var u in bm.Units)
        {
            if (u == null) continue;
            bool avail = u.IsAvailable;
            bool sel = avail && selectedForScout.Contains(u);
            var line = new Rect(rect.x + 8, yy, rect.width - 16, itemH - 4);

            GUI.DrawTexture(line, sel ? texCardSel : (avail ? texCardIdle : texCardLocked));
            DrawBorder(line, INK, 1);

            string mark = sel ? "[x]" : (avail ? "[ ]" : " - ");
            GUI.Label(new Rect(line.x + 10, line.y, 40, line.height), mark, nameStyle);
            GUI.Label(new Rect(line.x + 50, line.y, line.width - 60, line.height),
                u.UnitName + (avail ? "" : "   (на задании)"), nameStyle);

            if (avail && GUI.Button(line, "", GUIStyle.none))
            {
                if (sel) { selectedForScout.Remove(u); u.SetSelected(false); }
                else { selectedForScout.Add(u); u.SetSelected(true); }
            }
            yy += itemH;
        }
    }

    private void DrawScoutGear(BaseManagement bm, Rect rect)
    {
        float yy = rect.y + 14;
        DrawGearRow(rect, ref yy, "Оружие", ref scoutWeapons, bm.Weapons);
        DrawGearRow(rect, ref yy, "Провиант", ref scoutFood, bm.Food);
        DrawGearRow(rect, ref yy, "Аптечки", ref scoutMedkits, bm.Medkits);

        yy += 18;
        GUI.Label(new Rect(rect.x + 14, yy, rect.width - 28, 22), "Бойцы возьмут с собой:", body);
        yy += 24;
        GUI.Label(new Rect(rect.x + 14, yy, rect.width - 28, 20),
            $"  • оружия: {scoutWeapons}", body); yy += 22;
        GUI.Label(new Rect(rect.x + 14, yy, rect.width - 28, 20),
            $"  • провианта: {scoutFood}", body); yy += 22;
        GUI.Label(new Rect(rect.x + 14, yy, rect.width - 28, 20),
            $"  • аптечек: {scoutMedkits}", body);
    }

    private void DrawGearRow(Rect rect, ref float yy, string name, ref int amount, int available)
    {
        GUI.Label(new Rect(rect.x + 14, yy, 130, 32), name + ":", body);

        if (GUI.Button(new Rect(rect.x + 144, yy, 36, 32), "−", btn))
            amount = Mathf.Max(0, amount - 1);

        var valStyle = new GUIStyle(headerInk) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
        GUI.Label(new Rect(rect.x + 184, yy, 80, 32), $"{amount} / {available}", valStyle);

        if (GUI.Button(new Rect(rect.x + 268, yy, 36, 32), "+", btn))
            amount = Mathf.Min(available, amount + 1);

        yy += 40f;
    }

    private void DrawSendButton(BaseManagement bm, float x, float y, float w)
    {
        int count = selectedForScout.Count;
        bool canSend = count > 0 && scoutWeapons <= bm.Weapons && scoutFood <= bm.Food && scoutMedkits <= bm.Medkits;
        foreach (var u in selectedForScout) if (u == null || !u.IsAvailable) { canSend = false; break; }

        string text = count == 0
            ? "ВЫБЕРИ ХОТЯ БЫ ОДНОГО БОЙЦА"
            : $"▶  ВЫСЛАТЬ В ГОРОД  —  {count} боец(ов) × {bm.ScoutMissionDuration:0} сек";

        GUI.enabled = canSend;
        if (GUI.Button(new Rect(x, y, w, 64), text, btnAccept))
        {
            var list = new List<Unit>(selectedForScout);
            var m = bm.StartScoutMission(list, scoutWeapons, scoutFood, scoutMedkits);
            if (m != null)
            {
                selectedForScout.Clear();
                scoutWeapons = scoutFood = scoutMedkits = 0;
            }
        }
        GUI.enabled = true;
    }

    private void DrawStashTab(BaseManagement bm, Rect content)
    {
        float x = content.x + Pad + 10;
        float y = content.y + Pad + 10;
        GUI.Label(new Rect(x, y, 600, 30), "СКЛАД", headerInk);
        GUI.DrawTexture(new Rect(x, y + 32, 220, 2), MakeSolid(RUST));
        y += 44;
        GUI.Label(new Rect(x, y, 700, 20), "Имущество базы. Цифры в долге не оставляют.", small);
        y += 30;

        DrawStashRow(x, ref y, "Оружие — единиц", bm.Weapons, GOLD);
        DrawStashRow(x, ref y, "Провиант — пайков", bm.Food, SUCCESS_GREEN);
        DrawStashRow(x, ref y, "Аптечки — штук", bm.Medkits, RUST);

        y += 10;
        GUI.Label(new Rect(x, y, 600, 22), "Отладка:", small); y += 24;
        if (GUI.Button(new Rect(x, y, 200, 36), "+ оружие", btn)) bm.AddWeapons(1);
        if (GUI.Button(new Rect(x + 210, y, 200, 36), "+ провиант", btn)) bm.AddFood(1);
        if (GUI.Button(new Rect(x + 420, y, 200, 36), "+ аптечка", btn)) bm.AddMedkits(1);
    }

    private void DrawStashRow(float x, ref float y, string label, int value, Color accent)
    {
        var box = new Rect(x, y, 480, 50);
        GUI.DrawTexture(box, MakeSolid(new Color(0.95f, 0.86f, 0.65f)));
        DrawBorder(box, INK, 1);
        GUI.DrawTexture(new Rect(box.x, box.y, 6, box.height), MakeSolid(accent));

        GUI.Label(new Rect(box.x + 16, box.y, 300, box.height), label, body);
        var valStyle = new GUIStyle(headerInk) { fontSize = 26, alignment = TextAnchor.MiddleRight };
        GUI.Label(new Rect(box.xMax - 80, box.y, 70, box.height), value.ToString(), valStyle);
        y += 58;
    }

    private void DrawAttackTab(Rect content)
    {
        float x = content.x + Pad + 10;
        float y = content.y + Pad + 10;
        GUI.Label(new Rect(x, y, 600, 30), "АТАКА НА ВРАЖЕСКИЙ РАЙОН", headerInk);
        GUI.DrawTexture(new Rect(x, y + 32, 220, 2), MakeSolid(RUST));
        y += 50;

        var bigStamp = new GUIStyle(GUI.skin.label) { fontSize = 38, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        bigStamp.normal.textColor = new Color(RUST.r, RUST.g, RUST.b, 0.5f);
        GUI.Label(new Rect(content.x + 60, content.y + content.height * 0.4f - 30, content.width - 120, 60),
            "« В Р А З Р А Б О Т К Е »", bigStamp);

        var rotStamp = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
        rotStamp.normal.textColor = INK_FADED;
        GUI.Label(new Rect(content.x + 60, content.y + content.height * 0.4f + 40, content.width - 120, 30),
            "печать ШТАБА — утверждаю позже", rotStamp);
    }

    private void DrawBottomBar(BaseManagement bm)
    {
        float y = Screen.height - BottomH;
        var r = new Rect(0, y, Screen.width, BottomH);
        GUI.DrawTexture(r, texConcrete);
        GUI.DrawTexture(new Rect(0, y, Screen.width, 3), MakeSolid(new Color(0.05f, 0.05f, 0.04f)));
        GUI.DrawTexture(new Rect(0, y + 3, Screen.width, 1), texGold);

        GUI.Label(new Rect(Pad, y + 8, 600, 28), "АКТИВНЫЕ ВЫЛАЗКИ", headerCream);
        GUI.DrawTexture(new Rect(Pad, y + 38, 220, 2), texGold);

        float row = y + 50;
        if (bm.ActiveMissions.Count == 0)
        {
            GUI.Label(new Rect(Pad, row, 800, 24), "Тишина. Никого нет в городе.", smallCream);
            return;
        }

        foreach (var m in bm.ActiveMissions)
        {
            float progress = 1f - (m.TimeRemaining / m.TotalDuration);

            var rowR = new Rect(Pad, row, Screen.width - 2 * Pad, 28);
            var barBg = new Rect(rowR.x, rowR.y + 4, 380, 22);
            GUI.DrawTexture(barBg, MakeSolid(OLIVE_DARK));
            DrawBorder(barBg, new Color(GOLD.r, GOLD.g, GOLD.b, 0.6f), 1);
            GUI.DrawTexture(new Rect(barBg.x, barBg.y, barBg.width * progress, barBg.height), MakeSolid(RUST_BRIGHT));

            string txt = $"  Вылазка #{m.Id}  •  {m.Units.Count} боец(ов)  •  осталось {m.TimeRemaining:0.0} сек";
            var barTxt = new GUIStyle(mono) { alignment = TextAnchor.MiddleLeft, fontSize = 13 };
            GUI.Label(new Rect(barBg.x + 6, barBg.y, barBg.width - 6, barBg.height), txt, barTxt);

            string gear = $"взяли: оружие {m.WeaponsTaken}  /  провиант {m.FoodTaken}  /  аптечки {m.MedkitsTaken}";
            GUI.Label(new Rect(barBg.xMax + 16, barBg.y, 600, barBg.height), gear, smallCream);

            row += 30;
        }
    }
}
