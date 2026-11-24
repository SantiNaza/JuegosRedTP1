# La Orden: versión singleplayer para el TP de Optimización

Rama: `tp-optimizacion-singleplayer`. Es el mismo juego (zombies por oleadas, top-down, con cargadores, granadas, patada, pickups y puntos de mejora), pero **sin Photon** y con **código nuevo** que cumple las restricciones técnicas del TP. Los modelos, animaciones, audio, mapa y props del proyecto original se reutilizan.

## Cómo probarlo (primera vez)

1. Abrí el proyecto en Unity 2021.3.16f1 y esperá a que compile. Debería compilar sin errores.
2. Menú **TP → 1 - Configurar proyecto (capas)**: crea las capas `Enemy`, `Environment`, `Projectile` y `Player`.
3. Menú **TP → 2 - Generar escena y configs**: crea los prefabs limpios, los efectos, los configs, el mapa, el NavMesh y la escena `Assets/TP/Generated/TP_Game.unity`. También la deja en Build Settings.
4. Dale **Play**.

La escena se genera **sin optimizar** a propósito, para poder medir el "antes".

| Menú | Qué hace |
|---|---|
| TP → 3 - Aplicar optimizaciones de escena | Marca como static (para batching y occlusion) los pisos, paredes y props. Saca las sombras de los pisos, los props y las luces puntuales. Activa GPU instancing en los materiales. Pone skinning de 2 huesos en los zombies y acota la distancia de sombras. |
| TP → 3b - Quitar optimizaciones | Vuelve al estado sin optimizar, para comparar. |
| TP → 4 - Optimizar texturas y audio | Limita las texturas pesadas a 2048 y las comprime. La música pasa a streaming y los efectos se descomprimen al cargar. |

Controles: WASD para moverse, el mouse para apuntar, click izquierdo para disparar, click derecho para la patada, R para recargar, G para la granada (mantener para cocinarla), Shift para correr y Esc o P para pausar.

## Cómo cumple cada consigna

### Gameplay

| Consigna | Dónde |
|---|---|
| Waves consecutivas con dificultad creciente | `WaveSystem` y `WaveConfig`. Son 10 waves y en cada una hay más enemigos. Se suman tipos nuevos: rápidos desde la 2, tanques desde la 3 y divisores (se parten en 2 rápidos al morir) desde la 4. La vida y la velocidad suben con cada wave, el spawn es cada vez más rápido y aparece un **élite** en las waves 5 y 10. |
| Detectar fin de wave y pasar a la siguiente | `WaveSystem.CompleteWave`: cuando no quedan enemigos en cola ni vivos, muestra "¡OLEADA SUPERADA!" y arranca una cuenta regresiva automática. |
| Victoria solo al completar todas | `AllWavesClearedEvent` → `GameFlowService.EndMatch(true)` |
| Derrota | Vida del jugador en 0 → `PlayerDiedEvent` → pantalla de derrota |
| Enemigos con vida, movimiento, muerte, ataque | `EnemySystem`: persiguen con NavMesh, atacan cuerpo a cuerpo, reciben knockback y mueren con efecto |
| Ataque del jugador con proyectiles reutilizables | `WeaponSystem`, `ProjectileSystem` (con pool y trail) y `GrenadeSystem` (física, explosión en área) |
| Partículas, impactos, trails, flashes de luz | `VfxSystem`: fogonazo con luz, chispas en paredes, sangre, explosión con flash, muerte y pickup |
| Asset Store / assets externos | Zombies Mixamo (Mutant y Warzombie, con texturas pesadas: PNG de 8 a 30 MB), personajes, LowPoly Medieval Props, Barril |
| UI in-game | `HudScreen`: vida, stamina, wave, enemigos restantes, progreso, munición, cargadores, granadas, bajas, FPS y mensajes |
| Menú de inicio | `MenuScreen`: Jugar, Mejoras (puntos de stats), Personaje, Salir |
| Pausa real | `GameFlowService.TogglePause`: el UpdateManager deja de llamar a los sistemas pausables y además `Time.timeScale = 0`. Se puede reanudar, reiniciar o volver al menú. |

### Limitaciones técnicas

| Consigna | Cómo se cumple |
|---|---|
| Máximo 3 MonoBehaviours | Hay **1** solo: `GameBootstrap`. Las herramientas de editor son clases estáticas, no MonoBehaviours. |
| Custom Update Manager | `Core/UpdateManager.cs` maneja el registro y la baja de sistemas, el **orden**, la **frecuencia** (`interval`: los pickups corren cada 0,1 s), la **pausa** (sistemas pausables y no pausables) y la separación Update / FixedUpdate / LateUpdate. |
| Sin Update/FixedUpdate/LateUpdate | No hay ninguno en todo el código. El UpdateManager se inyecta en el **PlayerLoop** de Unity (`PlayerLoopInjector`). |
| Singleton único | `GameBootstrap.Instance`, solo para el composition root |
| Service Locator / DI | `ServiceLocator` (es una instancia, no estático) más inyección por constructor. Todo se arma en `GameBootstrap.Compose()`. |
| Sin referencias directas innecesarias | Los sistemas dependen de **interfaces** (`Core/Contracts.cs`) y se comunican por **eventos** (`EventBus`). Por ejemplo, `ProjectileSystem` solo conoce `IDamageService`. No se usan Find, FindObjectOfType ni búsquedas por tag. |
| Máximo 10 campos expuestos | Toda la configuración está en ScriptableObjects con 10 campos o menos. `GameBootstrap` tiene 2. |
| Entidades sin MonoBehaviour propio | `Enemy`, `Projectile`, `Grenade`, `Pickup` y `VfxInstance` son clases de datos. El GameObject es solo la vista. |
| Object Pooling | `Core/Pool.cs` para enemigos (un pool por tipo, precargado al cargar la escena), balas, granadas y cada tipo de efecto. Las balas usan SphereCast en vez de colliders y OnTrigger. |
| Luces, sombras, rendering | Luz direccional con sombras suaves, más de 20 luces puntuales, fog, materiales PBR y partículas |
| 2D/3D con carga real | El mapa tiene más de 1000 tiles, más de 100 props, hasta 45 zombies skinned con texturas pesadas, partículas y luces. Sin optimizar supera claramente los 500 draw calls. |

## Evidencia de profiling (qué capturar)

1. **Antes y después de los draw calls**: Con la escena sin optimizar (TP → 3b), jugá hasta la wave 4 o 5 y capturá el CPU Usage y el Rendering del Profiler, y también la ventana *Stats* (Batches y SetPass). Después aplicá TP → 3 y TP → 4 y repetí en la misma wave. Conviene medir en un **build Standalone** con *Development Build* y *Autoconnect Profiler*.
2. **GC**: en el CPU Usage, la columna *GC Alloc* durante el gameplay debería quedar en 0 B por frame en los sistemas propios. Los eventos son structs, el HUD usa `SetText` con números y las físicas usan las versiones NonAlloc.
3. **Memory Profiler**: instalá el paquete *Memory Profiler* desde el Package Manager. Tomá un snapshot en la wave 2 y otro en la wave 7 y compará. Con pooling, la cantidad de GameObjects y la memoria administrada se estabilizan.
4. **Pooling vs. Instantiate/Destroy**: en `Assets/TP/Generated/Configs/PerformanceConfig`, destildá **usePooling**. El mismo código pasa a crear y destruir en cada uso. Capturá el Profiler en la misma wave (se ven más *GC Alloc*, *Instantiate* y *Destroy*, y picos de frame time) y compará con usePooling activado.

## Estructura

```
Assets/TP/
  Scripts/Core/        UpdateManager, PlayerLoopInjector, ServiceLocator, EventBus, Pool, contratos y eventos
  Scripts/Config/      ScriptableObjects de configuración
  Scripts/Gameplay/    Sistemas: Player, Weapon, Grenade, Projectile, Enemy, Wave, Pickup, Vfx, Camera, Audio, Progression, GameFlow
  Scripts/UI/          UI por código (menú, mejoras, HUD, pausa, fin)
  Scripts/Bootstrap/   GameBootstrap (único MonoBehaviour y composition root)
  Editor/              Generador de escena/prefabs/configs y menú de optimizaciones
  Generated/           (lo crea el menú TP → 2)
```

Se eliminaron Photon, Remote Config, la API de Google, las escenas de lobby y todos los scripts viejos. Los prefabs originales quedaron en `Assets/Legacy` (antes era `Resources`), así no se incluyen en el build. El generador los toma de ahí y crea copias limpias.
