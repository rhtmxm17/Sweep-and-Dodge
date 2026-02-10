
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// <br/>Player GameObject가 "표현/입력/애니"를 담당
    /// <br/>ECS PlayerTag 엔티티(Proxy)에 Transform/입력을 밀어넣어 판정은 DOTS에서 처리
    /// <br/>단일 플레이어 전제: PlayerTag 싱글톤을 찾음(서브씬 로딩 타이밍 고려해 재시도 로직 포함)
    /// </summary>
    public sealed class PlayerEcsBridge : MonoBehaviour
    {
        [Header("Input")]
        public KeyCode VacuumKey = KeyCode.Space;

        [Header("Sync")]
        public bool SyncRotation = true;

        // Vacuum 상태 반영용 Animator (옵션)
        public Animator Animator;
        public string VacuumActiveBool = "VacuumActive";

        private EntityManager _em;
        private Entity _playerEntity;
        private bool _hasPlayerEntity;

        private void Awake()
        {
            TryBind();
        }

        private void Update()
        {
            if (!_hasPlayerEntity)
            {
                TryBind();
                if (!_hasPlayerEntity) return;
            }
            
            // GO -> ECS 동기화
            var sync = _em.GetComponentData<PlayerGoSyncComponent>(_playerEntity);
            sync.Position = transform.position;
            sync.SyncRotation = (byte)(SyncRotation ? 1 : 0);
            if (SyncRotation) sync.Rotation = transform.rotation;
            if (Input.GetKeyDown(VacuumKey)) sync.VacuumRequested = 1;
            _em.SetComponentData(_playerEntity, sync);

            // ECS -> GO : Vacuum 상태를 Animator에 반영(옵션)
            if (Animator != null)
            {
                var v = _em.GetComponentData<VacuumBurstComponent>(_playerEntity);
                Animator.SetBool(VacuumActiveBool, v.IsActive != 0);
            }
        }

        private void TryBind()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            _em = world.EntityManager;

            // PlayerTag 싱글톤 찾기 (서브씬 로딩 지연 대비)
            using var q = _em.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>());
            if (q.IsEmptyIgnoreFilter)
            {
                _hasPlayerEntity = false;
                return;
            }

            _playerEntity = q.GetSingletonEntity();
            _hasPlayerEntity = _playerEntity != Entity.Null;
        }
    }
}
