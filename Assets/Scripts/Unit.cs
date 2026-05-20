using UnityEngine;

public class Unit : MonoBehaviour
{
    public string UnitName { get; private set; }
    public bool IsOnMission { get; private set; }
    public bool IsSelected { get; private set; }
    public bool IsAvailable => !IsOnMission;

    private Vector3 basePosition;
    private Renderer rend;
    private Color baseColor;
    private Color selectedColor = new Color(1f, 0.92f, 0.3f);

    public void Init(string name, Vector3 position)
    {
        UnitName = name;
        basePosition = position;
        transform.position = position;
        rend = GetComponent<Renderer>();
        if (rend != null)
        {
            baseColor = Random.ColorHSV(0f, 1f, 0.35f, 0.6f, 0.7f, 0.95f);
            rend.material.color = baseColor;
        }
    }

    public void SetSelected(bool sel)
    {
        IsSelected = sel;
        UpdateColor();
    }

    private void UpdateColor()
    {
        if (rend == null) return;
        rend.material.color = IsSelected ? Color.Lerp(baseColor, selectedColor, 0.75f) : baseColor;
    }

    public void GoOnMission()
    {
        IsOnMission = true;
        IsSelected = false;
        gameObject.SetActive(false);
    }

    public void ReturnFromMission()
    {
        IsOnMission = false;
        transform.position = basePosition;
        gameObject.SetActive(true);
        UpdateColor();
    }
}
