using UnityEngine;

/// <summary>
/// Attach to the wall or any object that appears to move at runtime.
/// Logs the object's position before and after Play Mode starts.
/// </summary>
public class PositionDebugger : MonoBehaviour
{
    private Vector3 _editModePosition;
    private Quaternion _editModeRotation;
    private bool _isStationary = true;

    private void Awake()
    {
        _editModePosition = transform.position;
        _editModeRotation = transform.rotation;
    }

    private void Start()
    {
        Vector3 currentPos = transform.position;
        Quaternion currentRot = transform.rotation;
        
        if (currentPos != _editModePosition)
        {
            _isStationary = false;
            Debug.LogError($"[PositionDebugger] '{name}' MOVED at Start!\n" +
                $"  Edit mode: {_editModePosition}\n" +
                $"  Now:       {currentPos}\n" +
                $"  Delta:     {currentPos - _editModePosition}", this);
        }
        else
        {
            Debug.Log($"[PositionDebugger] '{name}' is STATIONARY (position unchanged).", this);
        }
        
        if (currentRot != _editModeRotation)
        {
            Debug.LogWarning($"[PositionDebugger] '{name}' ROTATED at Start!", this);
        }
    }

    private void Update()
    {
        if (_isStationary && transform.hasChanged)
        {
            Debug.Log($"[PositionDebugger] '{name}' CHANGED during gameplay: {transform.position}", this);
            transform.hasChanged = false;
        }
    }
}
