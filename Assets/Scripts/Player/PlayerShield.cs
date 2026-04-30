using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

[RequireComponent(typeof(PhotonView))]
public class PlayerShield : MonoBehaviourPun
{
    [Header("Shield References")]
    [SerializeField] private GameObject shieldObject;
    [SerializeField] private Transform shieldTransform;
    [SerializeField] private Transform shieldBlockPoint;
    [SerializeField] private Collider shieldCollider;

    [Header("Shield Settings")]
    [SerializeField] private float blockRadius = 0.7f;
    [SerializeField] private bool holdRightClick = true;
    [SerializeField] private bool showOnlyWhenActive = false;

    [Header("Optional Direction Check")]
    [SerializeField] private bool useSideCheck = false;
    [SerializeField] private float minSideDot = 0.2f;

    [Header("Animation Settings")]
    [SerializeField] private Vector3 activeRotationOffset = new Vector3(0f, 0f, 30f);
    [SerializeField] private float animationDuration = 0.15f;

    private PlayerHealth playerHealth;
    private bool isShieldActive;
    private Quaternion initialLocalRotation;
    private Coroutine animationCoroutine;

    public bool IsShieldActive => isShieldActive;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();

        if (shieldObject == null && shieldTransform != null)
        {
            shieldObject = shieldTransform.gameObject;
        }

        if (shieldTransform == null && shieldObject != null)
        {
            shieldTransform = shieldObject.transform;
        }

        if (shieldTransform != null)
        {
            initialLocalRotation = shieldTransform.localRotation;
        }

        ApplyShieldState();
    }

    private void Update()
    {
        if (!photonView.IsMine)
        {
            return;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            SetShieldActiveNetwork(false);
            return;
        }

        if (holdRightClick)
        {
            bool shouldUseShield = Input.GetMouseButton(1);

            if (shouldUseShield != isShieldActive)
            {
                SetShieldActiveNetwork(shouldUseShield);
            }
        }
        else
        {
            if (Input.GetMouseButtonDown(1))
            {
                SetShieldActiveNetwork(!isShieldActive);
            }
        }
    }

    private void SetShieldActiveNetwork(bool active)
    {
        photonView.RPC(nameof(RPC_SetShieldActive), RpcTarget.All, active);
    }

    [PunRPC]
    private void RPC_SetShieldActive(bool active)
    {
        isShieldActive = active;
        ApplyShieldState();
    }

    private void ApplyShieldState()
    {
        if (shieldObject != null && showOnlyWhenActive)
        {
            shieldObject.SetActive(isShieldActive);
        }

        if (shieldObject != null && !showOnlyWhenActive)
        {
            shieldObject.SetActive(true);
        }

        if (shieldCollider != null)
        {
            shieldCollider.enabled = isShieldActive;
        }

        if (shieldTransform != null && gameObject.activeInHierarchy)
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
            }
            animationCoroutine = StartCoroutine(AnimateShieldRotation(isShieldActive));
        }
    }

    private IEnumerator AnimateShieldRotation(bool isActivating)
    {
        Quaternion startRot = shieldTransform.localRotation;
        Quaternion targetRot = isActivating ? initialLocalRotation * Quaternion.Euler(activeRotationOffset) : initialLocalRotation;

        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            t = Mathf.SmoothStep(0f, 1f, t);

            shieldTransform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        shieldTransform.localRotation = targetRot;
    }

    public bool TryBlockDamage(Collider hitCollider, Vector3 attackPoint, Vector3 attackerPosition)
    {
        if (!isShieldActive)
        {
            return false;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            return false;
        }

        if (!WasShieldHit(hitCollider, attackPoint))
        {
            return false;
        }

        if (useSideCheck && !AttackComesFromShieldSide(attackerPosition))
        {
            return false;
        }

        Debug.Log("Ataque bloqueado con escudo");
        return true;
    }

    private bool WasShieldHit(Collider hitCollider, Vector3 attackPoint)
    {
        if (hitCollider != null && shieldCollider != null && hitCollider == shieldCollider)
        {
            return true;
        }

        if (hitCollider != null && shieldTransform != null && hitCollider.transform.IsChildOf(shieldTransform))
        {
            return true;
        }

        Vector3 blockCenter = GetShieldBlockPosition();

        float distance = Vector3.Distance(attackPoint, blockCenter);

        return distance <= blockRadius;
    }

    private bool AttackComesFromShieldSide(Vector3 attackerPosition)
    {
        Vector3 directionToAttacker = attackerPosition - transform.position;
        directionToAttacker.y = 0f;

        if (directionToAttacker.sqrMagnitude <= 0.01f)
        {
            return false;
        }

        directionToAttacker.Normalize();

        Vector3 shieldSideDirection = -transform.right;

        float dot = Vector3.Dot(shieldSideDirection, directionToAttacker);

        return dot >= minSideDot;
    }

    private Vector3 GetShieldBlockPosition()
    {
        if (shieldBlockPoint != null)
        {
            return shieldBlockPoint.position;
        }

        if (shieldTransform != null)
        {
            return shieldTransform.position;
        }

        return transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        Vector3 blockPosition = transform.position;

        if (shieldBlockPoint != null)
        {
            blockPosition = shieldBlockPoint.position;
        }
        else if (shieldTransform != null)
        {
            blockPosition = shieldTransform.position;
        }

        Gizmos.DrawWireSphere(blockPosition, blockRadius);
    }
}