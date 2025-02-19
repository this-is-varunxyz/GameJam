using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Windows.Speech;

public class PlayerMovement : MonoBehaviour
{
    public int forward_speed;
    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    public float rotationSpeed = 30f;
    public float xLimit = 0.6f;
    public float rotationAmount = 10f;
    public float rotationDuration = 0.2f;

    [Header("Vertical Movement Settings")]
    public float minYPosition = 0.2f;
    public float maxYPosition = 0.6f;
    public float stepSizeY = 0.2f;

    [Header("Microphone Settings")]
    public float minVoiceThreshold = 0.01f;
    private AudioSource micAudioSource;
    private bool micInitialized = false;
    private float currentVolume;

    [Header("Camera Settings")]
    public Camera mainCamera;
    public Vector3 cameraOffset = new Vector3(0f, 2f, -5f);
    public float cameraFollowSpeed = 10f;

    private float targetX;
    private float targetY;
    private Quaternion originalRotation;
    private bool isRotating = false;
    private Vector3 originalCameraRotation;
    private Coroutine currentRotationCoroutine;
    private KeywordRecognizer keywordRecognizer;
    private Dictionary<string, System.Action> voiceCommands = new Dictionary<string, System.Action>();

    void Start()
    {
        originalRotation = transform.rotation;
        targetX = transform.position.x;
        targetY = minYPosition;

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera != null)
        {
            originalCameraRotation = mainCamera.transform.eulerAngles;
        }

        SetupVoiceRecognition();
        InitializeMicrophone();
    }

    void SetupVoiceRecognition()
    {
        voiceCommands.Add("up", MoveUp);
        voiceCommands.Add("down", MoveDown);

        keywordRecognizer = new KeywordRecognizer(voiceCommands.Keys.ToArray(), ConfidenceLevel.Low);
        keywordRecognizer.OnPhraseRecognized += OnKeywordRecognized;
        keywordRecognizer.Start();
    }

    void InitializeMicrophone()
    {
        if (Microphone.devices.Length > 0)
        {
            micAudioSource = gameObject.AddComponent<AudioSource>();
            micAudioSource.clip = Microphone.Start(Microphone.devices[0], true, 1, 44100);
            micAudioSource.loop = true;
            micAudioSource.mute = true;
            micAudioSource.Play();
            micInitialized = true;
        }
    }

    float GetMicrophoneVolume()
    {
        if (!micInitialized) return 0f;

        float[] samples = new float[128];
        micAudioSource.GetOutputData(samples, 0);

        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += Mathf.Abs(samples[i]);
        }

        return sum / samples.Length;
    }

    void OnKeywordRecognized(PhraseRecognizedEventArgs args)
    {
        string recognizedText = args.text.ToLower();
        if (voiceCommands.ContainsKey(recognizedText))
        {
            voiceCommands[recognizedText].Invoke();
        }
    }

    void MoveUp()
    {
        targetY = Mathf.Min(targetY + stepSizeY, maxYPosition);
    }

    void MoveDown()
    {
        targetY = Mathf.Max(targetY - stepSizeY, minYPosition);
    }

    void Update()
    {
        HandleMovement();
        UpdateCamera();
        transform.position += new Vector3(0,0,forward_speed*Time.deltaTime);
    }
    

    void HandleMovement()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            targetX = Mathf.Max(-xLimit, transform.position.x - xLimit);
            StartNewRotation(-rotationAmount);
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            targetX = Mathf.Min(xLimit, transform.position.x + xLimit);
            StartNewRotation(rotationAmount);
        }

        Vector3 currentPosition = transform.position;
        float newX = Mathf.Lerp(currentPosition.x, targetX, moveSpeed * Time.deltaTime);
        float newY = Mathf.Lerp(currentPosition.y, targetY, moveSpeed * Time.deltaTime);
        transform.position = new Vector3(newX, newY, currentPosition.z);
    }

    void StartNewRotation(float yRotation)
    {
        if (currentRotationCoroutine != null)
        {
            StopCoroutine(currentRotationCoroutine);
        }
        currentRotationCoroutine = StartCoroutine(TemporaryRotation(yRotation));
    }

    void UpdateCamera()
    {
        if (mainCamera != null)
        {
            Vector3 desiredPosition = transform.position + cameraOffset;
            mainCamera.transform.position = Vector3.Lerp(
                mainCamera.transform.position, 
                desiredPosition, 
                cameraFollowSpeed * Time.deltaTime
            );
            mainCamera.transform.eulerAngles = originalCameraRotation;
        }
    }

    IEnumerator TemporaryRotation(float yRotation)
    {
        isRotating = true;
        Quaternion startRotation = transform.rotation;
        Vector3 currentEuler = startRotation.eulerAngles;
        Quaternion targetRotation = Quaternion.Euler(currentEuler.x, yRotation, currentEuler.z);
        float elapsedTime = 0f;

        while (elapsedTime < rotationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / rotationDuration;
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        transform.rotation = originalRotation;
        isRotating = false;
        currentRotationCoroutine = null;
    }

    void OnDestroy()
    {
        if (keywordRecognizer != null)
        {
            keywordRecognizer.Stop();
            keywordRecognizer.Dispose();
        }

        if (micInitialized)
        {
            Microphone.End(Microphone.devices[0]);
        }
    }
}
