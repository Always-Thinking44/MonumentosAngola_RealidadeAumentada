# Monumentos de Angola — Realidade Aumentada

Aplicação de **realidade aumentada** que dá vida a um mapa de Angola e aos seus monumentos.
Aponta-se a câmara para uma imagem impressa e a aplicação mostra, em 3D e com som, o património
cultural angolano.

> Projeto académico — Aijá Itel, 12.ª Classe · PPM

---

## Índice

- [Visão do utilizador](#visão-do-utilizador)
  - [Como funciona](#como-funciona)
  - [Mapa de Angola](#mapa-de-angola)
  - [Painel do monumento](#painel-do-monumento)
  - [Que monumentos existem](#que-monumentos-existem)
- [Visão do desenvolvedor](#visão-do-desenvolvedor)
  - [Requisitos](#requisitos)
  - [Arquitetura](#arquitetura)
  - [Fluxo de execução](#fluxo-de-execução)
  - [Estrutura de pastas](#estrutura-de-pastas)
  - [Alvos Vuforia](#alvos-vuforia)
  - [Como adicionar um monumento](#como-adicionar-um-monumento)
  - [Como adicionar áudio](#como-adicionar-áudio)
  - [Detalhes técnicos](#detalhes-técnicos)
- [Como clonar e correr o projeto](#como-clonar-e-correr-o-projeto)
- [Limitações conhecidas](#limitações-conhecidas)

---

## Visão do utilizador

### Como funciona

1. **Imprime** (ou abre noutro ecrã) as imagens-alvo que acompanham o projeto — o mapa de Angola
   e as imagens dos monumentos.
2. **Aponta a câmara** do telemóvel/PC para uma delas.
3. Quando a aplicação reconhece a imagem, aparece o **painel correspondente** por cima do vídeo
   da câmara, com um modelo 3D a rodar.
4. Se afastares a câmara da imagem, o painel desaparece sozinho.

Não é preciso instalar nada além da aplicação: o reconhecimento é feito no dispositivo.

### Mapa de Angola

Ao apontar a câmara para a imagem do **mapa de Angola**, abre-se um painel grande com o país em 3D,
a rodar lentamente, e uma breve introdução ao território.

| Controlo | O que faz |
|---|---|
| **Parar giro / Continuar giro** | Pausa ou retoma a rotação do mapa |
| **II / >** | Pausa ou retoma a narração e a música de fundo |
| **Tocar numa província** | Abre os monumentos dessa província |
| **X** | Fecha o painel |

O mapa roda sempre, por isso um toque durante o movimento pode apanhar uma província vizinha.
Se quiseres precisão, carrega em **Parar giro** antes de tocar.

Províncias sem monumentos associados mostram uma mensagem do género
*«Uíge» ainda não tem monumentos nesta aplicação.*

### Painel do monumento

Ao tocar numa província com monumentos, abre-se **diretamente o primeiro monumento** dessa
província. No topo do painel aparece uma barra de navegação:

```
  <        Malanje · 1/2        >          Voltar ao mapa
```

| Controlo | O que faz |
|---|---|
| **<** / **>** | Passa para o monumento anterior / seguinte da mesma província |
| **Voltar ao mapa** | Regressa ao mapa de Angola |
| **II / >** | Pausa ou retoma o áudio |
| **↺ / ↻** | Inverte o sentido de rotação do modelo 3D |
| **+ / −** | Aproxima ou afasta o modelo 3D |
| **X** | Fecha tudo e volta à câmara |

Se a província só tiver um monumento, as setas ficam desativadas.

### Que monumentos existem

| Província | Monumento(s) |
|---|---|
| Luanda | Soldado Desconhecido |
| Malanje | Palanca Negra Gigante · Marimba |
| Zaire | Mbanza Kongo |
| Lunda Norte · Lunda Sul · Moxico | O Pensador · Mwana Pwo |
| Huambo · Bié | Monte Luvili |

---

## Visão do desenvolvedor

### Requisitos

| Ferramenta | Versão |
|---|---|
| Unity Editor | **6000.4.4f1** (Unity 6) |
| Universal Render Pipeline (URP) | 17.4.0 |
| Vuforia Engine | 11.4.4 (incluído em `Packages/`) |
| glTFast | 6.20.0 (importação dos modelos `.glb`) |
| TextMesh Pro | via `com.unity.ugui` 2.0.0 |
| Git LFS | **obrigatório** (ver [clonagem](#como-clonar-e-correr-o-projeto)) |

O projeto usa o **novo Input System** (`InputSystemUIInputModule`).

### Arquitetura

A aplicação segue um padrão simples de **alvo Vuforia → UI singleton**:

```
ObserverBehaviour (Vuforia)
        │  OnTargetStatusChanged
        ▼
MonumentTarget  ──────────────►  MonumentUI.Instance.Mostrar()
AngolaTarget    ──────────────►  AngolaUI.Instance.Mostrar()
                                        │
                                        │ (só Angola)
                                        ▼
                              AngolaMapClick → raio → ProvinciaAngola
                                        │
                                        ▼
                              MonumentUI.Instance.Mostrar()
```

#### Scripts (`Assets/Scripts/`)

| Ficheiro | Papel |
|---|---|
| `MonumentTarget.cs` | Dados de um monumento (nome, descrição, data, localização, modelo, áudio). Escuta o `ObserverBehaviour` e mostra/esconde o painel. |
| `MonumentUI.cs` | Painel de um monumento: título, descrição, data, localização, pré-visualização 3D (câmara + `RenderTexture`), zoom, áudio. Singleton. |
| `AngolaTarget.cs` | Equivalente ao `MonumentTarget`, mas para o mapa: título, descrição, modelo, rotação inicial, velocidade de giro, áudio. |
| `AngolaUI.cs` | Constrói em runtime o painel do mapa, o palco 3D e a barra de navegação. Gere giro, áudio, toque nas províncias e navegação entre monumentos. Singleton. |
| `AngolaMapClick.cs` | Ponte entre o clique na UI e o 3D: converte o toque no `RawImage` num raio da câmara de pré-visualização. Implementa `IPointerClickHandler`. |
| `ProvinciaAngola.cs` | Marca um mesh do mapa como província (`nome`). |

#### Porque é que a UI é construída em runtime

O painel de Angola (fundo, textos, botões, palco 3D, câmara) é criado por código em `AngolaUI`
em vez de estar montado na cena. Vantagens: a cena fica limpa, o layout está num único sítio e
não há referências partidas ao duplicar o projeto. O painel dos monumentos, esse, já existia na
cena e é reaproveitado.

#### Como as províncias são detetadas

O modelo `Angola_Map.glb` traz um mesh por província (`Province_Luanda`, `Province_Malanje`, …).
Em runtime, `AngolaUI.PrepararProvincias()`:

1. percorre os filhos do modelo à procura de `Province_*`;
2. adiciona um `MeshCollider` a cada um;
3. adiciona `ProvinciaAngola` com o nome legível (ex.: `Lunda_Norte` → `Lunda Norte`).

O toque chega assim:

```
RawImage (MapaView)
  → AngolaMapClick.OnPointerClick
    → coordenadas normalizadas (u, v)
      → Camera.ViewportPointToRay
        → Physics.Raycast (layer "Preview")
          → ProvinciaAngola.nome
            → AngolaUI.AbrirProvincia
```

> **Nota:** é obrigatório que `AngolaMapClick` declare `IPointerClickHandler`. Sem a interface,
> o `EventSystem` nunca invoca `OnPointerClick` e o clique é ignorado em silêncio.

#### Ligação província → monumento

Definida em `AngolaUI.monumentosPorProvincia`, por **nome** do `MonumentTarget`:

```csharp
{ "Luanda",  new[] { "Soldado Desconhecido" } },
{ "Malanje", new[] { "Palanca Negra Gigante", "Marimba" } },
...
```

A correspondência é feita em runtime com `FindObjectsByType<MonumentTarget>(FindObjectsInactive.Include)`,
por isso os alvos dos monumentos podem estar desativados na cena.

### Fluxo de execução

1. `VuforiaBehaviour` inicializa e carrega o dataset
   `Assets/StreamingAssets/Vuforia/ProjectoMonumentosAngolateste.xml`.
2. Cada `ImageTarget_*` tem um `ObserverBehaviour` e um `MonumentTarget` (ou `AngolaTarget`).
3. Quando o estado passa a `TRACKED`, o script chama `Mostrar()` no singleton de UI respetivo.
4. `Mostrar()` instancia o modelo num palco isolado (layer `Preview`), longe da câmara AR,
   e liga uma câmara dedicada que desenha para uma `RenderTexture` mostrada num `RawImage`.
5. Ao perder o alvo (`!TRACKED`), o painel fecha.

> Só o estado `TRACKED` conta — `EXTENDED_TRACKED` manteria o painel aberto depois de perder a
> imagem.

### Estrutura de pastas

```
Assets/
├── AngolaMap/              Angola_Map.glb (mapa com um mesh por província)
├── audio/                  Narrações e músicas (mp3/m4a)
├── monumentosAngolanos/    Modelos .glb dos monumentos
├── Scenes/SampleScene.unity  Cena principal (única cena usada)
├── Scripts/                Código da aplicação
├── Settings/               Configuração URP
├── StreamingAssets/Vuforia/
│   ├── ProjectoMonumentosAngolateste.xml   Lista de alvos
│   └── ProjectoMonumentosAngolateste.dat   Base de dados de reconhecimento
└── TextMesh Pro/           Recursos TMP
Packages/
└── com.ptc.vuforia.engine-11.4.4.tgz   Vuforia (132 MB, em Git LFS)
```

### Alvos Vuforia

Todos os alvos estão em `Assets/StreamingAssets/Vuforia/ProjectoMonumentosAngolateste.xml`:

| Nome do alvo | Onde é usado |
|---|---|
| `angola` | `ImageTarget_angola` → `AngolaTarget` |
| `monumento_soldados_vuforia_target` | Soldado Desconhecido |
| `palanca` | Palanca Negra Gigante |
| `marimba` | Marimba |
| `mbanza` | Mbanza Kongo |
| `montanha_luvili_vuforia_target` | Monte Luvili |
| `pensador` | O Pensador |
| `mwana` | Mwana Pwo |

Cada `ImageTarget_*` na cena tem `mTrackableName` igual ao nome acima e dimensões a condizer com
o tamanho declarado no XML.

### Como adicionar um monumento

1. **Importa o modelo** para `Assets/monumentosAngolanos/` (`.glb` — o glTFast trata do resto).
2. **Cria um Image Target** na cena: duplica um `ImageTarget_*` existente, muda o
   `mTrackableName` e ajusta largura/altura ao alvo.
3. **Adiciona o componente** `MonumentTarget` ao novo Image Target e preenche:
   - `nome` — tem de ser **igual** ao usado em `monumentosPorProvincia`;
   - `descricao`, `data`, `localizacao`;
   - `modeloPrefab` — o `.glb` importado;
   - `rotacaoInicial` — se o modelo aparecer deitado/virado;
   - `narracao` / `musicaFundo`.
4. **Associa à província** em `Assets/Scripts/AngolaUI.cs`:

```csharp
{ "Huíla", new[] { "Cristo Rei do Lubango" } },
```

5. **Adiciona o alvo ao dataset Vuforia** (Vuforia Engine → Image Targets, no editor) e faz
   *Build/Update* para regenerar o `.dat`.

### Como adicionar áudio

Os campos `narracao` e `musicaFundo` existem tanto em `MonumentTarget` como em `AngolaTarget`.
Os do mapa estão **vazios** — basta arrastar os clipes no Inspector.

Os dois `AudioSource` (voz e fundo) são criados em runtime pelo `MonumentUI`/`AngolaUI`, com
`spatialBlend = 0` (2D), por isso não é preciso configurar nada na cena. O volume da música de
fundo é `volumeFundo` (0.25 por defeito).

### Detalhes técnicos

| Aspeto | Valor |
|---|---|
| Canvas | 1920×1080, Screen Space Overlay, `CanvasScaler` *Scale With Screen Size* (match 0.5) |
| Layer de pré-visualização | `Preview` (8) |
| RenderTexture do mapa | 1024×1024 |
| Câmara do mapa | z = −4.6, FOV 45° |
| Painel do mapa | 840×840 px |
| `tamanhoAlvo` do mapa | 3.2 (maior que o dos monumentos, 2.0) |
| `rotacaoInicial` do mapa | (90, 180, 0) |
| Palco do mapa | y ≈ −2000 (o dos monumentos fica em y = −1000) |
| Velocidade de giro | 20 °/s (configurável por `AngolaTarget.velocidadeGiro`) |

O palco fica muito abaixo da câmara AR para que os modelos de pré-visualização nunca apareçam no
vídeo da câmara real.

---

## Como clonar e correr o projeto

> ⚠️ **O repositório usa Git LFS.** Os modelos `.glb`, o áudio, as imagens e o pacote Vuforia
> (132 MB) estão guardados no LFS. **Sem o Git LFS instalado**, esses ficheiros são descarregados
> como ponteiros de texto de ~130 bytes e o projeto não abre.

### 1. Instalar o Git LFS (uma vez por máquina)

```bash
git lfs install
```

### 2. Clonar

```bash
git clone https://github.com/Always-Thinking44/MonumentosAngola_RealidadeAumentada.git
cd MonumentosAngola_RealidadeAumentada
```

### 3. Confirmar que o LFS descarregou tudo

```bash
git lfs ls-files    # deve listar ~55 ficheiros
```

Se aparecerem ficheiros com `-` em vez de `*`, corre:

```bash
git lfs pull
```

### 4. Abrir no Unity

1. Abre o **Unity Hub** → *Add project from disk* → escolhe a pasta clonada.
2. Usa o Unity **6000.4.4f1** (ou deixa o Hub instalar a versão indicada em
   `ProjectSettings/ProjectVersion.txt`).
3. Na primeira abertura o Unity importa os assets e resolve os pacotes — pode demorar alguns
   minutos por causa do Vuforia.
4. Abre `Assets/Scenes/SampleScene.unity` e carrega em **Play**.

### 5. Testar sem imprimir nada

O Vuforia pode usar a **webcam** como fonte de vídeo (Play Mode → Vuforia Configuration →
*Webcam*). Basta mostrar a imagem-alvo à câmara do computador.

> Em Play Mode, o Vuforia por vezes falha ao reinicializar a webcam após um *domain reload*.
> Se acontecer, sai e volta a entrar em Play Mode.

### Publicar alterações

```bash
git add -A
git commit -m "Descrição da alteração"
git push
```

Se adicionares um **novo tipo de ficheiro binário** que ainda não esteja coberto, acrescenta a
regra ao `.gitattributes` **antes** do primeiro commit desse ficheiro:

```
*.webp  filter=lfs diff=lfs merge=lfs -text
```

---

## Limitações conhecidas

- **Narração e música do mapa** ainda não estão atribuídas (`AngolaTarget.narracao` /
  `musicaFundo` vazios) — o áudio do mapa fica em silêncio até serem preenchidos.
- **Províncias sem monumentos** (Cabinda, Uíge, Bengo, Cuanza Norte, Cuanza Sul, Benguela, Huíla,
  Cuando Cubango, Namibe, Cunene) apenas mostram uma mensagem informativa.
- **O mapa está sempre a rodar** por defeito; tocar numa província em movimento pode apanhar um
  vizinho. O botão *Parar giro* resolve.
- Existe um duplicado do modelo do mapa em `Assets/Editor/Angola_Map.glb` (não usado em runtime).

---

## Créditos

- Modelos 3D dos monumentos e do mapa — ver `Assets/monumentosAngolanos/` e `Assets/AngolaMap/`.
- Áudio — ver `Assets/audio/`.
- Vuforia Engine © PTC.
