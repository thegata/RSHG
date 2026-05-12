using UnityEngine;

public class DialogueNPC : MonoBehaviour
{
    [Tooltip("Имя, которое будет показано над репликой.")]
    public string npcName = "Человек";

    [TextArea(2, 5)]
    [Tooltip("Первая (и пока единственная) реплика NPC.")]
    public string dialogueLine = "Привет";

    [Tooltip("Точка, куда сфокусируется камера игрока. Если пусто — берётся верх объекта.")]
    public Transform lookTarget;

    public Vector3 GetLookPoint()
    {
        if (lookTarget != null) return lookTarget.position;

        var col = GetComponentInChildren<Collider>();
        if (col != null) return col.bounds.center + Vector3.up * (col.bounds.extents.y * 0.6f);

        return transform.position + Vector3.up * 1.6f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.8f);
        Gizmos.DrawWireSphere(GetLookPoint(), 0.08f);
    }
}
