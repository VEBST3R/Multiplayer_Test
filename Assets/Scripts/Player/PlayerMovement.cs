using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float gravity = -9.81f;

    private CharacterController m_CharacterController;
    private PlayerInput m_PlayerInput;
    private ThirdPersonCamera m_CameraController;
    
    private Vector3 m_PlayerVelocity;
    private bool m_IsGrounded;

    public override void OnNetworkSpawn()
    {
        m_CharacterController = GetComponent<CharacterController>();
        m_PlayerInput = GetComponent<PlayerInput>();
        m_CameraController = GetComponent<ThirdPersonCamera>();
        
        // КРИТИЧНО: Тільки власник (Local Player) керує CharacterController
        // Клони (інші гравці) керуються через ClientNetworkTransform
        if (m_CharacterController != null)
        {
            m_CharacterController.enabled = IsOwner;
        }
        else
        {
            Debug.LogError("CharacterController is missing on Player!");
        }
        
        gameObject.layer = LayerMask.NameToLayer("Player");
    }

    private void Update()
    {
        // Якщо це не наш гравець - нічого не робимо (позицію оновить ClientNetworkTransform автоматично)
        if (!IsOwner) return;
        
        HandleMovement();
    }

    private void HandleMovement()
    {
        if (m_CharacterController == null) return;

        m_IsGrounded = m_CharacterController.isGrounded;

        // Гравітація (скидання швидкості на землі)
        if (m_IsGrounded && m_PlayerVelocity.y < 0)
        {
            m_PlayerVelocity.y = -2f;
        }

        // Отримання вводу
        Vector2 input = m_PlayerInput != null ? m_PlayerInput.MoveInput : Vector2.zero;
        Vector3 moveDirection = Vector3.zero;

        // Розрахунок напрямку відносно камери
        if (input.magnitude > 0.1f)
        {
            // Намагаємось знайти камеру
            Vector3 cameraForward = Vector3.forward;
            Vector3 cameraRight = Vector3.right;

            if (m_CameraController != null) 
            {
                cameraForward = m_CameraController.GetCameraForward();
                cameraRight = m_CameraController.GetCameraRight();
            }
            else if (Camera.main != null)
            {
                cameraForward = Camera.main.transform.forward;
                cameraRight = Camera.main.transform.right;
            }

            // Ігноруємо нахил камери вверх/вниз для руху по площині
            cameraForward.y = 0;
            cameraRight.y = 0;
            cameraForward.Normalize();
            cameraRight.Normalize();

            moveDirection = (cameraForward * input.y + cameraRight * input.x).normalized;
        }

        // Рух
        Vector3 velocity = moveDirection * moveSpeed;
        
        // Застосування гравітації
        m_PlayerVelocity.y += gravity * Time.deltaTime;
        
        // Фактичний рух контролера
        m_CharacterController.Move((velocity + m_PlayerVelocity) * Time.deltaTime);

        // Поворот персонажа в сторону руху
        if (moveDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
}
