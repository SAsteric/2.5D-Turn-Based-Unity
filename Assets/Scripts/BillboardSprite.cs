using UnityEngine;

/// <summary>
/// Attach to the sprite child GameObject so it always faces the main camera.
/// This is the key component that makes 2D sprites look correct in a 3D world
/// (the "billboard" or "paper doll" technique used in Octopath Traveller).
///
/// The sprite is NOT locked to the Y-axis — it fully faces the camera
/// so it looks correct even if the camera pitches up or down.
/// </summary>
public class BillboardSprite : MonoBehaviour
{
    [Header("Billboard Settings")]
    [Tooltip("Lock rotation to Y-axis only (keeps sprite upright). " +
             "Uncheck for full camera-facing billboard.")]
    [SerializeField] private bool lockYAxisOnly = false;

    private Camera _cam;

    private void Start()
    {
        _cam = Camera.main;
    }

    private void LateUpdate()
    {
        if (_cam == null)
        {
            _cam = Camera.main;
            return;
        }

        if (lockYAxisOnly)
        {
            // Only rotate around Y — sprite stays perfectly upright
            float yAngle = _cam.transform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, yAngle, 0f);
        }
        else
        {
            // Full billboard: sprite plane always perpendicular to camera view ray
            transform.rotation = Quaternion.LookRotation(
                -_cam.transform.forward,
                _cam.transform.up
            );
        }
    }
}
