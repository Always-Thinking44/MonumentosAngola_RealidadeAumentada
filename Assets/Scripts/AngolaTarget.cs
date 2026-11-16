using UnityEngine;
using Vuforia;

// Colocar no Image Target "angola" (ao lado do ObserverBehaviour).
[RequireComponent(typeof(ObserverBehaviour))]
public class AngolaTarget : MonoBehaviour
{
    [Header("Informação")]
    public string titulo = "Angola";
    [TextArea(6, 14)] public string descricao;

    [Header("Mapa 3D")]
    public GameObject modeloPrefab;              // Angola_Map.glb
    [Tooltip("(90, 180, 0) põe o mapa de frente para a câmara, com o norte para cima.")]
    public Vector3 rotacaoInicial = new Vector3(90f, 180f, 0f);
    public float tamanhoAlvo = 3.2f;             // maior dimensão do mapa (maior que os monumentos)
    public float velocidadeGiro = 20f;           // graus por segundo

    [Header("Áudio")]
    public AudioClip narracao;
    public AudioClip musicaFundo;

    [Header("Comportamento")]
    public bool esconderAoPerderTarget = true;

    ObserverBehaviour observer;

    void Awake()
    {
        observer = GetComponent<ObserverBehaviour>();
        observer.OnTargetStatusChanged += OnStatusChanged;
    }

    void OnDestroy()
    {
        if (observer != null)
            observer.OnTargetStatusChanged -= OnStatusChanged;
    }

    void OnStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
    {
        if (AngolaUI.Instance == null) return;

        // Enquanto o utilizador lê um monumento aberto no mapa, o estado deste alvo
        // não interfere: o painel do monumento mantém-se aberto.
        if (AngolaUI.Instance.EstaANavegarMonumento) return;

        // Só TRACKED: evita manter o painel do mapa depois de perder a imagem
        bool detectado = status.Status == Status.TRACKED;

        if (detectado)
            AngolaUI.Instance.Mostrar(this);
        else if (esconderAoPerderTarget)
            AngolaUI.Instance.AoPerderTarget(this);
    }
}
