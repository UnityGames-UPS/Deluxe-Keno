# Game Script Access Modifier Guide - Before Migration

This guide provides one reusable process for reviewing `public`, `internal`, and `private` declarations in a standalone game before platform migration.

The review changes only the selected game's scripts. It does not add platform integration, alter platform code, or perform migration work.

The objective is to reduce unnecessary visibility without breaking Unity serialization, editor discovery, JavaScript/native bridges, animation events, UnityEvents, reflection, or backend serialization. Use `private` when only the declaring class uses a member and `internal` when another script in the selected game uses it. Do not add getters, setters, properties, or wrapper methods solely to change visibility.

---

## 1. Scope

Limit inspection and edits to the selected game's script folder:

```text
<GameDir>/Scripts
```

Do not change:

- Scripts belonging to another game.
- Scenes, prefabs, animations, JavaScript files, packages, or project settings.
- Namespaces, assembly definitions, build settings, AssetBundle settings, GUIDs, or asset ownership.
- Member names, signatures, serialized field names, or runtime behavior.

Treat these integration-sensitive files as outside the routine access-modifier pass when they exist:

- `ImageAnimation.cs`: animation- and serialized-binding entry points.
- `OrientationChange.cs`: device and orientation integration.
- `JSFunctCalls.cs`: JavaScript, WebGL, and native bridge declarations.

Leave every existing access modifier in those files unchanged. If a visibility problem is found there, record it and handle it as a separate integration task with the relevant serialized assets or external caller in scope.

In every file included in the review, do not change a `[Serializable]` class declaration or anything declared inside that class, including its fields, properties, methods, constructors, nested types, and other members. For example, leave `AuthTokenData` and its `cookie`, `socketURL`, and `nameSpace` fields unchanged. All other declarations are eligible for review under the normal access rules unless another rule in this guide excludes them.

Because non-script callers are outside this review, leave any possible JavaScript, reflection, `SendMessage`, UnityEvent, or Animation Event entry point `public`.

---

## 2. Access Decision Table

Classify each declaration using the first matching rule:

| Situation | Target access |
| :--- | :--- |
| Required interface implementation | `public` |
| External API or possible external callback | `public` |
| Unity Editor-discovered type, override, or callback | Preserve the visibility required by Unity |
| `[Serializable]` class declaration and everything inside its class block | Leave unchanged |
| Used by another script in the selected game | `internal` |
| Used only by the declaring class | `private` |
| Inspector field used by another game script | `[SerializeField] internal` |
| Inspector field used only by the declaring class | `[SerializeField] private` |
| Serialized DTO member | Preserve the visibility required by its serializer |

Do not perform a global replacement. Review each declaration against its callers and serialization requirements.

---

## 3. Special Cases

### Unity component types

Keep scene and prefab `MonoBehaviour` classes public. Do not change component type visibility during routine member cleanup.

Plain helper types used only by the game's scripts may become `internal`. Do not move or nest types during this pass.

### Inspector fields

Use `[SerializeField] private` when only the declaring class uses the field:

```csharp
[SerializeField] private Button spinButton;
[SerializeField] private GameObject quitPopup;
```

Use `[SerializeField] internal` when another script in the selected game directly accesses the field:

```csharp
[SerializeField] internal Button spinButton;
```

Keep the existing field name so Unity retains its serialized value. Do not add a getter, setter, property, or wrapper solely for this cleanup.

If a rename is separately approved, use `[FormerlySerializedAs("oldName")]` and verify every affected scene and prefab in Unity. Renaming is not part of this access-modifier pass.

### Unity lifecycle methods

Use `private` when only Unity invokes the method and no other selected-game script calls it:

```csharp
private void Awake()
private void Start()
private void OnEnable()
private void OnDisable()
private void OnDestroy()
private void Update()
```

Use `internal` when another script in the selected game directly calls the method.

### Interface methods

Interface implementations remain public. C# requires ordinary interface implementations to be public, including EventSystem handlers:

```csharp
public void OnPointerEnter(PointerEventData eventData)
public void OnPointerExit(PointerEventData eventData)
public void OnPointerDown(PointerEventData eventData)
public void OnPointerUp(PointerEventData eventData)
public void OnBeginDrag(PointerEventData eventData)
public void OnDrag(PointerEventData eventData)
public void OnEndDrag(PointerEventData eventData)
```

### UnityEvent and animation callbacks

Methods selected in a Button, Toggle, UnityEvent, or Animation Event are stored by name in serialized assets. Treat them as external entry points and keep them `public` unless the corresponding scene, prefab, animation, and event bindings have been explicitly brought into scope and proven absent.

### Unity Editor integration

Preserve the visibility required for Unity Editor discovery, custom editors, property drawers, menu commands, and overrides. For example, a custom Editor type and its `public override void OnInspectorGUI()` method must remain externally discoverable.

### DTOs and serializers

Identify the serializer before changing a DTO:

- `JsonUtility` serializes public fields automatically. Non-public fields require `[SerializeField]`.
- Newtonsoft.Json normally serializes public fields and properties. Non-public members require an explicit supported contract such as `[JsonProperty]`.
- Names and JSON attributes may be part of a backend contract.

Do not change a `[Serializable]` class declaration or anything inside its class block during this pass, even when ordinary C# caller searches suggest narrower access. This restriction covers fields, properties, methods, constructors, nested types, and all other members. This includes classes such as:

```csharp
[Serializable]
public class AuthTokenData
{
    public string cookie;
    public string socketURL;
    public string nameSpace;
}
```

Apply this restriction in every reviewed file, including `SocketIOManager.cs` and `GameDataModels.cs`. Declarations that are not part of a `[Serializable]` class block remain eligible for review. Do not add accessors, rename DTO members, or convert fields to properties.

---

## 4. Review Procedure

The inspection and edit scope is limited to `<GameDir>/Scripts`, excluding `ImageAnimation.cs`, `OrientationChange.cs`, and `JSFunctCalls.cs`. `SocketIOManager.cs` and `GameDataModels.cs` are included in the review under the same rules as other files. Do not inspect or modify platform code, another game, scenes, prefabs, animations, JavaScript files, or other project areas during this pass. Because those non-script callers are not audited, any member that may be an external callback or serialized method target remains `public`.

### Step 1: Establish the integration boundary

Identify socket-, authentication-, serializer-, animation-, orientation-, JavaScript-, native-, reflection-, and Unity-owned entry points before classifying members. Exclude every declaration in `ImageAnimation.cs`, `OrientationChange.cs`, and `JSFunctCalls.cs`. In every other reviewed file, mark each complete `[Serializable]` class block as unchanged.

If ownership is uncertain, leave the declaration unchanged until its callers and bindings can be reviewed in a separately scoped task.

### Step 2: Inventory public declarations

Review all public declarations in the selected game's scripts, excluding `ImageAnimation.cs`, `OrientationChange.cs`, and `JSFunctCalls.cs`. Include both `SocketIOManager.cs` and `GameDataModels.cs`. In every reviewed file, skip each `[Serializable]` class declaration and its entire class block, then review all remaining eligible declarations normally.

### Step 3: Check callers in the selected game

Check every use of the member within the selected game's scripts. Use `private` when only the declaring class uses the member. Use `internal` when another selected-game script uses it.

### Step 4: Check serialization in the selected game

Within the selected game's scripts, locate serialization and deserialization calls involving the type. Confirm the serializer before changing eligible declarations. Do not modify any `[Serializable]` class block.

### Step 5: Record the decision

| File | Type/member | Integration-owned? | Script callers | Serializer/Inspector | Target | Evidence |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| Example | `ExampleType.value` | No | Declaring class only | Inspector | `[SerializeField] private` | Script search completed |

### Step 6: Apply a small batch

Change only access modifiers and any `[SerializeField]` attribute needed to preserve Unity serialization.

Do not combine this work with renaming, dead-code removal, behavior changes, architecture changes, formatting sweeps, or migration work.

---

## 5. Validation

1. Confirm the standalone game scripts compile without accessibility errors.
2. Open the game scene and verify that components and Inspector references are intact.
3. Verify buttons, toggles, pointer handlers, drag handlers, UnityEvents, Animation Events, and custom Editor behavior.
4. Test authentication, initial data, gameplay, backend responses, orientation, focus, and exit flows supported by the standalone game.
5. Review the final diff and confirm that only the selected game's scripts changed.
6. Confirm the diff contains only approved access modifiers and serialization attributes.

---

## 6. Completion Checklist

- [ ] Only the selected game's scripts were inspected and modified.
- [ ] `SocketIOManager.cs` and `GameDataModels.cs` were reviewed under the normal rules.
- [ ] Every `[Serializable]` class declaration and everything inside its class block was left unchanged in every reviewed file.
- [ ] `ImageAnimation.cs`, `OrientationChange.cs`, and `JSFunctCalls.cs` were left unchanged.
- [ ] Interface implementations and possible external callbacks remain public.
- [ ] UnityEvent, Animation Event, reflection, `SendMessage`, JavaScript/native bridge, and Editor entry points remain callable.
- [ ] Members used by another selected-game script use `internal` where appropriate.
- [ ] Members used only by their declaring class use `private` where appropriate.
- [ ] Inspector values and serialized DTO contracts are preserved.
- [ ] No accessors, renames, behavior changes, or unrelated refactors were added.
- [ ] The game compiled and its required standalone flows were validated.

---

## 7. Principle

Before migration, keep external contracts, serialized callbacks, Editor integration, and interface implementations `public`, use `internal` for members shared by the selected game's scripts, and use `private` for members used only by their declaring class. Never alter a `[Serializable]` class declaration or anything inside its class block; review all other in-scope declarations normally.
