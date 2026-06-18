using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class PlayerStatsApplier : MonoBehaviourPun
{
    private void Start()
    {
        // Solo aplico MIS stats a MI jugador. Los clones remotos no tocan nada.
        if (!photonView.IsMine) return;

        var weapon = GetComponent<TopDownWeaponController>();
        var health = GetComponent<HealthSystem>();
        var movement = GetComponent<PlayerMovement>();

        float L(StatType s) => PlayerStatsConfig.GetLevel(s) * PlayerStatsConfig.PerPoint(s);

        // --- Arma (usa el método que ya existía) ---
        if (weapon != null)
        {
            weapon.AplicarMejoras(
                L(StatType.Damage),
                L(StatType.FireRate),
                (int)L(StatType.MagCapacity),
                L(StatType.ReloadSpeed)
            );
        }

        // --- Agente ---
        if (health != null)
        {
            health.AplicarMejorasAgente(
                L(StatType.MaxHealth),
                L(StatType.ReviveSpeed),
                L(StatType.BleedOutTime)
            );
        }

        if (movement != null)
        {
            movement.AplicarMejoraVelocidad(L(StatType.MoveSpeed));
        }
    }
}