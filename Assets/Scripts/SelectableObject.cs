using UnityEngine;

public class SelectableObject : MonoBehaviour
{
    private GameObject selectionRing;

    public void SetRing(GameObject ringPrefab)
    {
        if (ringPrefab == null)
            return;

        if (selectionRing == null)
        {
            selectionRing = Instantiate(ringPrefab, transform);
            selectionRing.transform.localPosition = Vector3.zero;
            selectionRing.transform.localRotation = Quaternion.identity;
        }

        selectionRing.SetActive(false);
    }

    public void Select()
    {
        if (selectionRing != null)
            selectionRing.SetActive(true);
    }

    public void Deselect()
    {
        if (selectionRing != null)
            selectionRing.SetActive(false);
    }
}