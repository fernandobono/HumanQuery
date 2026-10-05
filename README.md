# HumanQuery — Preguntale a Northwind en castellano

**Sistemas de Datos II — ESCMB — Unidad 4: IA y Datos**

Una API que recibe una pregunta en lenguaje natural ("¿cuántos clientes hay por país?"), le pide a una IA que escriba el SQL, lo ejecuta sobre Northwind y le pide a la IA que redacte la respuesta.

![Arquitectura](wwwroot/humanquery/Resumen%20IA%20POC%20.jpeg)

1. El usuario hace la consulta desde el navegador o desde un GPT.
2. El backend le manda a la IA la pregunta y la **estructura** de la base (tablas y columnas, sin datos).
3. La IA devuelve una sentencia SQL.
4. El backend ejecuta ese SQL en SQL Server.
5. La base devuelve los resultados crudos.
6. El backend le manda esos resultados a la IA, que los devuelve "humanizados".
7. El usuario recibe la respuesta.

> 🔎 Para pensar: en el paso 2 no se mandan datos… pero en el paso 6 sí. ¿Qué implica eso si la base tuviera datos sensibles?

---

## Qué necesitás

| Requisito | Para qué |
|---|---|
| [.NET SDK 8 o superior](https://dotnet.microsoft.com/download) | Compilar y correr la API |
| SQL Server con **Northwind** | La base que se consulta (la misma de la Unidad 1) |
| Una clave de **Google Gemini** (gratis) | La IA que escribe el SQL y redacta la respuesta |

---

## Paso a paso

### 1. Crear el usuario de solo lectura

La API ejecuta SQL que escribió una IA. Por eso se conecta con un usuario que **solo puede leer**: si la IA genera un `DELETE`, la base lo rechaza.

Abrí `sql/crear-usuario-lectura.sql` en SSMS, DBeaver o Azure Data Studio, conectado como `sa`, y ejecutalo.

### 2. Sacar la clave de Gemini

1. Entrá a <https://aistudio.google.com/apikey> con tu cuenta de Google.
2. Tocá **Create API key** y copiala.

### 3. Guardar la clave (nunca en el código)

Desde la carpeta del proyecto:

```bash
dotnet user-secrets set "OpenAI:ApiKey" "PEGÁ_ACÁ_TU_CLAVE"
```

La clave queda guardada en tu usuario del sistema, fuera del repositorio. **Nunca la subas a GitHub.**

### 4. Revisar la conexión a la base

En `appsettings.json`, `DefaultConnection` apunta a `localhost,1433` con el usuario `ia_lector`. Si tu SQL Server está en otro lugar (por ejemplo `localhost\SQLEXPRESS`), cambiá `Data Source`.

### 5. Correr

```bash
dotnet run
```

Se abre el navegador en <http://localhost:5126/humanquery/humanquery.html>.

| Página | Qué muestra |
|---|---|
| `/humanquery/humanquery.html` | La caja para preguntar: respuesta, SQL generado y datos crudos |
| `/humanquery/monitor.html` | Todo lo que se le mandó a la IA en cada paso |
| `/humanquery/learn.html` | Explicación del código |
| `/swagger` | La API para probarla a mano |

---

## Usarlo desde un GPT de ChatGPT

Un GPT puede llamar a esta API con una **Acción**. ChatGPT necesita una dirección pública, así que se abre un túnel con [ngrok](https://ngrok.com/download):

```bash
ngrok http 5126
```

1. Copiá la dirección `https://….ngrok-free.app` que muestra ngrok.
2. En el GPT: **Configurar → Acciones → Crear nueva acción**.
3. Pegá el contenido de `gpt/openapi-humanquery.yaml`, reemplazando la `url` de `servers` por la de ngrok.
4. Autenticación: **Ninguna**.

> ⚠️ Mientras el túnel está abierto, cualquiera con la dirección puede usar tu API (y tu cuota de Gemini). Cerralo cuando termines.

---

## Sobre la IA

El código habla el formato de la API de OpenAI, que Gemini también entiende. Por eso alcanza con cambiar `OpenAI:BaseUrl`, `OpenAI:Model` y la clave para usar OpenAI u otro proveedor compatible.

Si un modelo está saturado o fue retirado, la API prueba solo los de `OpenAI:FallbackModels`.

## Estructura

```
Controllers/HumanQueryController.cs   ← el flujo completo (pasos 2 a 7)
Controllers/MonitoringController.cs   ← historial para monitor.html
Services/OpenAiService.cs             ← llamada a la IA, con reintentos y modelos de respaldo
Services/QueryLogService.cs           ← guarda en memoria cada consulta
wwwroot/humanquery/                   ← páginas del navegador
sql/crear-usuario-lectura.sql         ← usuario de solo lectura
gpt/openapi-humanquery.yaml           ← esquema para la Acción del GPT
```
