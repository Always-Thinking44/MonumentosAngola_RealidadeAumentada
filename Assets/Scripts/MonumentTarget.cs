using UnityEngine;
using Vuforia;

// Colocar em cada Image Target (ao lado do ObserverBehaviour).
[RequireComponent(typeof(ObserverBehaviour))]
public class MonumentTarget : MonoBehaviour
{
    [Header("Informação")]
    public string nome;
    [TextArea(4, 12)] public string descricao;
    public string data;
    public string localizacao;

    [Header("3D e áudio")]
    public GameObject modeloPrefab;
    [Tooltip("Corrige a orientação do modelo (ex.: palanca 180,0,0)")]
    public Vector3 rotacaoInicial;
    public AudioClip narracao;
    public AudioClip musicaFundo;

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
        // Nos monumentos, perder a imagem NÃO fecha nada: o painel, o modelo 3D
        // e o áudio continuam a aparecer. Só fecham no botão X do painel
        // (ou em "Voltar ao mapa", nos monumentos abertos a partir do mapa).
        if (status.Status != Status.TRACKED) return;

        if (MonumentUI.Instance != null)
            MonumentUI.Instance.Mostrar(this);
    }
}
