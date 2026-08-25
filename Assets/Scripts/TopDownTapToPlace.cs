using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// The 2D (top-down) placement condition.
// It mirrors ARTapToPlaceObject, but instead of AR plane detection it uses a fixed
// overhead ORTHOGRAPHIC camera and intersects taps with the floor plane by maths.
// It reuses the SAME furniture prefabs, the SAME selection ring, and the SAME
// PlacementLogger — so the CSV it produces matches the AR export exactly.
//
// Works with mouse (editor testing) and touch (on device) via the Input System's Pointer.
public class TopDownTapToPlace : MonoBehaviour
{
    [SerializeField] private GameObject selectedPrefab;
    [SerializeField] private GameObject selectionRingPrefab;
    [SerializeField] private PlacementLogger logger;

    [SerializeField] private Camera topDownCamera;   // overhead orthographic camera
    [SerializeField] private float groundY = 0f;     // height of the floor plane

    [SerializeField] private float minScale = 0.3f;
    [SerializeField] private float maxScale = 3.0f;
    [SerializeField] private float scaleStep = 0.1f; // change per Bigger/Smaller press

    private readonly List<GameObject> placedObjects = new List<GameObject>();
    private GameObject selectedPlacedObject;
    private SelectableObject selectedObject;

    private bool isDragging = false;

    // Called by your palette buttons, same as in the AR scene.
    public void SelectObject(GameObject prefab) { selectedPrefab = prefab; }

    public void ResetPlacedObjects()
    {
        foreach (GameObject obj in placedObjects)
            if (obj != null) Destroy(obj);

        if (logger != null) logger.UnregisterAll();

        placedObjects.Clear();
        selectedPlacedObject = null;
        selectedObject = null;
    }

    public void DeleteSelectedObject()
    {
        if (selectedPlacedObject != null)
        {
            if (logger != null) logger.UnregisterPlacement(selectedPlacedObject);
            placedObjects.Remove(selectedPlacedObject);
            Destroy(selectedPlacedObject);
            selectedPlacedObject = null;
            selectedObject = null;
        }
    }

    public void RotateSelectedLeft()
    {
        if (selectedPlacedObject != null)
        {
            selectedPlacedObject.transform.Rotate(0f, -15f, 0f);
            if (logger != null) logger.CountAdjustment();
        }
    }

    public void RotateSelectedRight()
    {
        if (selectedPlacedObject != null)
        {
            selectedPlacedObject.transform.Rotate(0f, 15f, 0f);
            if (logger != null) logger.CountAdjustment();
        }
    }

    // Wire "Bigger" / "Smaller" buttons to these (AR uses pinch; buttons keep 2D simple).
    public void ScaleSelectedUp()   { ApplyScale(1f + scaleStep); }
    public void ScaleSelectedDown() { ApplyScale(1f - scaleStep); }

    private void ApplyScale(float factor)
    {
        if (selectedPlacedObject == null) return;
        float s = Mathf.Clamp(selectedPlacedObject.transform.localScale.x * factor, minScale, maxScale);
        selectedPlacedObject.transform.localScale = new Vector3(s, s, s);
        if (logger != null) logger.CountAdjustment();
    }

    private bool GetGroundPoint(Vector2 screenPos, out Vector3 point)
    {
        Plane ground = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
        Ray ray = topDownCamera.ScreenPointToRay(screenPos);
        if (ground.Raycast(ray, out float enter))
        {
            point = ray.GetPoint(enter);
            return true;
        }
        point = Vector3.zero;
        return false;
    }

    private void SelectObjectInScene(GameObject objectToSelect)
    {
        if (selectedObject != null) selectedObject.Deselect();
        selectedPlacedObject = objectToSelect;

        SelectableObject selectable = objectToSelect.GetComponent<SelectableObject>();
        if (selectable == null)
        {
            selectable = objectToSelect.AddComponent<SelectableObject>();
            selectable.SetRing(selectionRingPrefab);
        }
        selectedObject = selectable;
        selectedObject.Select();
    }

    private void Update()
    {
        if (Pointer.current == null || topDownCamera == null) return;

        Vector2 screenPos = Pointer.current.position.ReadValue();

        // Ignore taps that are over UI (buttons), like the AR script does.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (Pointer.current.press.wasPressedThisFrame)
        {
            // First: did we tap an already-placed object? If so, select + start dragging it.
            Ray ray = topDownCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                GameObject hitObject = hit.collider.transform.root.gameObject;
                if (placedObjects.Contains(hitObject))
                {
                    SelectObjectInScene(hitObject);
                    isDragging = true;
                    if (logger != null) logger.CountAdjustment();
                    return;
                }
            }

            // Otherwise place the currently-selected prefab on the floor.
            if (selectedPrefab == null) return;

            if (GetGroundPoint(screenPos, out Vector3 groundPoint))
            {
                GameObject newObject = Instantiate(selectedPrefab, groundPoint, Quaternion.identity);
                placedObjects.Add(newObject);
                if (logger != null) logger.RegisterPlacement(newObject, selectedPrefab.name);
                SelectObjectInScene(newObject);
                isDragging = false;
            }
        }

        // Drag the selected object across the floor while the pointer is held.
        if (Pointer.current.press.isPressed && isDragging && selectedPlacedObject != null)
        {
            if (GetGroundPoint(screenPos, out Vector3 groundPoint))
                selectedPlacedObject.transform.position = groundPoint;
        }

        if (Pointer.current.press.wasReleasedThisFrame)
            isDragging = false;
    }
}
