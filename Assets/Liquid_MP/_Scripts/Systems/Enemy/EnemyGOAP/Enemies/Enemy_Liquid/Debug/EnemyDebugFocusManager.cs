using System.Collections.Generic;
using UnityEngine;

public class EnemyDebugFocusManager : MonoBehaviour
{
    public static EnemyDebugFocusManager Instance { get; private set; }

    #region Variables
    [Header("Focus Controls")]
    [SerializeField] private bool autoFocusFirstSeen = true;
    [SerializeField] private KeyCode cycleFocusKey = KeyCode.Tab;
    [SerializeField] private bool clickToFocus = true;

    [Header("Click Selection")]
    [SerializeField] private Camera selectionCamera;
    [SerializeField] private float maxClickDistance = 250f;
    [Tooltip("Only colliders on these layers can be clicked to focus.")]
    [SerializeField] private LayerMask clickMask = ~0;

    [Header("Registration")]
    [Tooltip("If enabled, manager will auto-find existing debug targets.")]
    [SerializeField] private bool autoRegisterExistingTargets = true;
    [SerializeField] private bool rescanOneFrameAfterStart = true;

    private readonly List<IEnemyDebugTarget> _targets = new List<IEnemyDebugTarget>();
    private int _focusedIndex = -1;
    #endregion

    public IEnemyDebugTarget FocusedTarget
    {
        get
        {
            if (_focusedIndex < 0 || _focusedIndex >= _targets.Count)
            {
                return null;
            }
            return _targets[_focusedIndex];
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (selectionCamera == null)
        {
            selectionCamera = Camera.main;
        }

        if (autoRegisterExistingTargets)
        {
            RegisterAllExistingTargets();
        }
    }

    private void Start()
    {
        if (rescanOneFrameAfterStart && autoRegisterExistingTargets)
        {
            StartCoroutine(RescanNextFrame());
        }
    }

    private System.Collections.IEnumerator RescanNextFrame()
    {
        yield return null;
        RegisterAllExistingTargets();
    }

    private void Update()
    {
        if (Input.GetKeyDown(cycleFocusKey))
        {
            FocusNext();
        }

        if (clickToFocus && Input.GetMouseButtonDown(0))
        {
            TryFocusFromClick();
        }

        if (_targets.Count == 0)
        {
            _focusedIndex = -1;
        }
        else if (_focusedIndex >= _targets.Count)
        {
            _focusedIndex = _targets.Count - 1;
        }
    }

    public void Register(IEnemyDebugTarget target)
    {
        if (target == null)
        {
            return;
        }

        if (_targets.Contains(target))
        {
            return;
        }

        _targets.Add(target);

        if (autoFocusFirstSeen && _focusedIndex < 0)
        {
            _focusedIndex = 0;
        }
    }

    public void Unregister(IEnemyDebugTarget target)
    {
        if (target == null)
        {
            return;
        }

        int index = _targets.IndexOf(target);
        if (index < 0)
        {
            return;
        }

        _targets.RemoveAt(index);

        if (_targets.Count == 0)
        {
            _focusedIndex = -1;
            return;
        }

        if (_focusedIndex >= _targets.Count)
        {
            _focusedIndex = _targets.Count - 1;
        }
    }

    public bool IsFocused(IEnemyDebugTarget target)
    {
        return target != null && target == FocusedTarget;
    }

    public void FocusNext()
    {
        if (_targets.Count == 0)
        {
            _focusedIndex = -1;
            return;
        }

        _focusedIndex++;
        if (_focusedIndex >= _targets.Count)
        {
            _focusedIndex = 0;
        }
    }

    public void Focus(IEnemyDebugTarget target)
    {
        if (target == null)
        {
            return;
        }

        int index = _targets.IndexOf(target);
        if (index >= 0)
        {
            _focusedIndex = index;
        }
    }

    private void RegisterAllExistingTargets()
    {
        // Find all MonoBehaviours and register any that implement IEnemyDebugTarget.
        // This is only done at startup / rescans, so cost is fine.
        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IEnemyDebugTarget t)
            {
                Register(t);
            }
        }
    }

    private void TryFocusFromClick()
    {
        if (selectionCamera == null)
        {
            selectionCamera = Camera.main;
            if (selectionCamera == null)
            {
                return;
            }
        }

        Ray ray = selectionCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, maxClickDistance, clickMask, QueryTriggerInteraction.Ignore))
        {
            Transform t = hit.collider.transform;
            while (t != null)
            {
                MonoBehaviour[] behaviours = t.GetComponents<MonoBehaviour>();
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is IEnemyDebugTarget debugTarget)
                    {
                        Focus(debugTarget);
                        return;
                    }
                }

                t = t.parent;
            }
        }
    }
}