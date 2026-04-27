using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;


[RequireComponent(typeof(PhotonView))]
public class PlayerAttack : MonoBehaviourPun
{
    [Header("Attack Settings")]
    [SerializeField] private float damage = 20f;
    [SerializeField] private float cooldown = 1f;
    [SerializeField] private float attackRadius = 0.45f;
    [SerializeField] private LayerMask hittableLayers;

    [Header("Spear Movement")]
    [SerializeField] private Transform spearTransform;
    [SerializeField] private Transform spearHitPoint;
    [SerializeField] private float spearForwardDistance = 1.2f;
    [SerializeField] private float spearForwardTime = 0.12f;
    [SerializeField] private float spearReturnTime = 0.18f;

    [Header("Debug")]
    [SerializeField] private bool drawHitGizmo = true;

    private Vector3 spearStartLocalPosition;
    private bool isAttacking;
    private float nextAttackTime;
    private Coroutine spearRoutine;

    private readonly HashSet<PlayerHealth> damagedPlayers = new HashSet<PlayerHealth>();

    private void Awake()
    {
        if (spearTransform != null)
        {
            spearStartLocalPosition = spearTransform.localPosition;
        }
    }

    private void Update()
    {
        if (!photonView.IsMine)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            TryAttack();
        }
    }

    private void TryAttack()
    {
        if (isAttacking)
        {
            return;
        }

        if (Time.time < nextAttackTime)
        {
            return;
        }

        nextAttackTime = Time.time + cooldown;

        photonView.RPC(nameof(RPC_PlayAttack), RpcTarget.All);

        StartCoroutine(CheckDamageDuringAttack());
    }

    [PunRPC]
    private void RPC_PlayAttack()
    {
        if (spearTransform == null)
        {
            Debug.LogWarning("No hay Spear Transform asignado en PlayerAttack.");
            return;
        }

        if (spearRoutine != null)
        {
            StopCoroutine(spearRoutine);
        }

        spearRoutine = StartCoroutine(SpearAttackMovement());
    }

    private IEnumerator SpearAttackMovement()
    {
        isAttacking = true;

        Vector3 startPosition = spearStartLocalPosition;
        Vector3 targetPosition = spearStartLocalPosition + Vector3.forward * spearForwardDistance;

        float timer = 0f;

        while (timer < spearForwardTime)
        {
            timer += Time.deltaTime;

            float t = timer / spearForwardTime;
            spearTransform.localPosition = Vector3.Lerp(startPosition, targetPosition, t);

            yield return null;
        }

        spearTransform.localPosition = targetPosition;

        timer = 0f;

        while (timer < spearReturnTime)
        {
            timer += Time.deltaTime;

            float t = timer / spearReturnTime;
            spearTransform.localPosition = Vector3.Lerp(targetPosition, startPosition, t);

            yield return null;
        }

        spearTransform.localPosition = startPosition;

        isAttacking = false;
    }

    private IEnumerator CheckDamageDuringAttack()
    {
        damagedPlayers.Clear();

        float activeHitTime = spearForwardTime + 0.05f;
        float timer = 0f;

        while (timer < activeHitTime)
        {
            timer += Time.deltaTime;

            CheckHit();

            yield return null;
        }
    }

    private void CheckHit()
    {
        Vector3 hitPosition = GetHitPosition();

        Collider[] hits = Physics.OverlapSphere(
            hitPosition,
            attackRadius,
            hittableLayers
        );

        foreach (Collider hit in hits)
        {
            PlayerHealth targetHealth = hit.GetComponentInParent<PlayerHealth>();

            if (targetHealth == null)
            {
                continue;
            }

            PhotonView targetView = targetHealth.GetComponent<PhotonView>();

            if (targetView == null)
            {
                continue;
            }

            if (targetView.ViewID == photonView.ViewID)
            {
                continue;
            }

            if (damagedPlayers.Contains(targetHealth))
            {
                continue;
            }

            damagedPlayers.Add(targetHealth);

            targetHealth.TakeDamage(damage);

            Debug.Log("Ataque con lanza. Daño aplicado: " + damage);
        }
    }

    private Vector3 GetHitPosition()
    {
        if (spearHitPoint != null)
        {
            return spearHitPoint.position;
        }

        return transform.position + transform.forward * spearForwardDistance;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawHitGizmo)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetHitPosition(), attackRadius);
    }
}