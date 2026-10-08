using UnityEngine;

// Noble Six: first-person Spartan with recharging shield, two scavengeable weapon slots, melee, frag grenades and sprint.
[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }

    [Header("Movement")]
    public float walkSpeed = 7f;
    public float sprintMultiplier = 1.7f;
    public float jumpHeight = 1.6f;
    public float gravity = -22f;
    public float lookSensitivity = 2.2f;
    public float baseFov = 80f;
    public float crouchSpeedMultiplier = 0.5f;

    [Header("Vitals")]
    public float maxShield = 100f;
    public float maxHealth = 50f;
    public float shieldRechargeDelay = 4f;
    public float shieldRechargeRate = 45f;

    [Header("Melee")]
    public float meleeDamage = 70f;
    public float meleeRange = 2.6f;
    public float meleeCooldown = 0.7f;

    class Slot { public WeaponDef def; public int ammo, reserve; }

    public float Shield { get; private set; }
    public float Health { get; private set; }
    public int Grenades { get; private set; } = 2;
    public bool IsReloading { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsZoomed { get; private set; }
    public bool IsCrouched { get; private set; }
    public bool HasDropShield { get; private set; }
    public Vector3 EyePos { get { return cam.transform.position; } }
    public float HitFlash { get; private set; }
    public float HitMarker { get; private set; }
    public string Prompt { get; private set; } = "";
    public string WeaponName => Cur.def.name;
    public bool IsMeleeWeapon => Cur.def.melee;
    public float Zoom => Cur.def.zoom;
    public int Ammo => Cur.ammo;
    public int Reserve => Cur.reserve;

    readonly Slot[] slots = new Slot[2];
    int cur;
    Slot Cur => slots[cur];

    CharacterController cc;
    Camera cam;
    Transform gun;
    Light muzzle;
    Transform muzzlePoint, gunModel;
    Vector3 impulse;
    float pitch, yVel, nextShot, nextMelee, lastDamageTime, reloadEnd, gunKick, sprintBlend;
    bool rechargeSfx;

    static Slot NewSlot(WeaponDef d) { return new Slot { def = d, ammo = d.mag, reserve = d.reserve }; }

    void Awake()
    {
        Instance = this;
        cc = GetComponent<CharacterController>();
        cam = GetComponentInChildren<Camera>();
        gun = cam.transform.Find("Gun");
        if (gun)
        {
            var ml = new GameObject("MuzzleLight");
            ml.transform.SetParent(gun, false);
            muzzle = ml.AddComponent<Light>(); muzzle.type = LightType.Point; muzzle.range = 9f; muzzle.color = new Color(1f, 0.7f, 0.3f); muzzle.intensity = 0f;
        }
        Shield = maxShield; Health = maxHealth;
        slots[0] = NewSlot(Weapons.Dmr);       // Noble Six starts with the DMR and the Magnum
        slots[1] = NewSlot(Weapons.Pistol);
        ShowModel();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Each weapon has its own model prefab (Resources/Weapons); the same prefab is used for pickups on the ground
    void ShowModel()
    {
        if (!gun) return;
        if (gunModel) Destroy(gunModel.gameObject);
        var prefab = Resources.Load<GameObject>("Weapons/" + Cur.def.id);
        if (prefab)
        {
            gunModel = Instantiate(prefab, gun).transform;
            gunModel.localPosition = Vector3.zero; gunModel.localRotation = Quaternion.identity;
            foreach (var l in gunModel.GetComponentsInChildren<Light>()) l.enabled = false;
            muzzlePoint = gunModel.Find("Muzzle");
            ViewArms.Pose(gun, gunModel);
        }
        else muzzlePoint = null;
        if (muzzle) { muzzle.transform.SetParent(muzzlePoint ? muzzlePoint : gun, false); muzzle.transform.localPosition = Vector3.zero; }
    }

    void Update()
    {
        if (IsDead) return;
        Look();
        Move();
        Weapon();
        Interact();
        RechargeShield();
        HitFlash = Mathf.MoveTowards(HitFlash, 0f, Time.deltaTime * 2f);
        HitMarker = Mathf.MoveTowards(HitMarker, 0f, Time.deltaTime * 5f);
        if (muzzle) muzzle.intensity = Mathf.MoveTowards(muzzle.intensity, 0f, Time.deltaTime * 40f);

        // Zoom (hold right mouse) on scoped weapons
        IsZoomed = !IsSprinting && !IsReloading && Cur.def.zoom > 1f && Input.GetMouseButton(1);
        float fovTarget = IsZoomed ? baseFov / Cur.def.zoom : baseFov + (IsSprinting ? 7f : 0f);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fovTarget, Time.deltaTime * 10f);
        if (gun) gun.gameObject.SetActive(!(IsZoomed && Cur.def.zoom >= 4f));

        // Gun recoil recovery
        gunKick = Mathf.Lerp(gunKick, 0f, Time.deltaTime * 15f);
        sprintBlend = Mathf.MoveTowards(sprintBlend, IsSprinting ? 1f : 0f, Time.deltaTime * 6f);
        if (gun)
        {
            // Weapon lowers and tilts while sprinting
            gun.localPosition = new Vector3(0.3f, -0.28f - 0.1f * sprintBlend, 0.55f - gunKick - 0.05f * sprintBlend);
            gun.localRotation = Quaternion.Euler(22f * sprintBlend, -14f * sprintBlend, 0f);
        }

        if (Input.GetKeyDown(KeyCode.X) && HasDropShield) { HasDropShield = false; DropShield.Deploy(transform.position + transform.forward * 1.2f + Vector3.up * 0.9f); }
        UpdateCrouch();
        if (Input.GetKeyDown(KeyCode.Escape)) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }

    // Crouching lowers the camera and capsule: hide behind cover and the Covenant lose sight of you
    void UpdateCrouch()
    {
        IsCrouched = (Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl));
        float targetH = IsCrouched ? 1.1f : 1.9f;
        cc.height = Mathf.MoveTowards(cc.height, targetH, Time.deltaTime * 6f);
        cc.center = new Vector3(0f, cc.height / 2f, 0f);
        float camY = Mathf.Lerp(1.0f, 1.7f, Mathf.InverseLerp(1.1f, 1.9f, cc.height));
        cam.transform.localPosition = new Vector3(0f, camY, 0f);
    }

    void Look()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;
        float sens = lookSensitivity / (IsZoomed ? Cur.def.zoom : 1f);
        transform.Rotate(0f, Input.GetAxis("Mouse X") * sens, 0f);
        pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * sens, -85f, 85f);
        cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move()
    {
        float fwd = Input.GetAxisRaw("Vertical");
        Vector3 input = transform.right * Input.GetAxisRaw("Horizontal") + transform.forward * fwd;
        if (input.sqrMagnitude > 1f) input.Normalize();
        // Hold Shift while moving forward to sprint; the weapon is lowered so you can't shoot
        IsSprinting = Input.GetKey(KeyCode.LeftShift) && fwd > 0.1f && !IsCrouched && !IsZoomed && cc.isGrounded;
        float speed = walkSpeed * Cur.def.moveMul * (IsCrouched ? crouchSpeedMultiplier : IsSprinting ? sprintMultiplier : 1f);

        if (cc.isGrounded && yVel < 0f) yVel = -2f;
        if (cc.isGrounded && Input.GetButtonDown("Jump")) yVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
        yVel += gravity * Time.deltaTime;

        cc.Move((input * speed + Vector3.up * yVel + impulse) * Time.deltaTime);
        impulse = Vector3.Lerp(impulse, Vector3.zero, Time.deltaTime * 6f);
    }

    // ---------- Weapons ----------

    void Weapon()
    {
        var s = Cur; var d = s.def;
        if (IsReloading && Time.time >= reloadEnd) FinishReload(s);

        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchTo(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchTo(1);
        else if (Input.GetKeyDown(KeyCode.Tab) || Input.GetAxis("Mouse ScrollWheel") != 0f) SwitchTo(1 - cur);
        s = Cur; d = s.def;

        if (Input.GetKeyDown(KeyCode.F) && Time.time >= nextMelee) Melee();   // melee works mid-sprint (lunge)
        if (Input.GetKeyDown(KeyCode.G) && Grenades > 0) ThrowGrenade();

        bool locked = Cursor.lockState != CursorLockMode.Locked || IsSprinting;
        if (d.melee)
        {
            if (Input.GetMouseButtonDown(0) && !locked && Time.time >= nextShot) SwordSwing(d);
            return;
        }

        if (Input.GetKeyDown(KeyCode.R) && !IsReloading && s.ammo < d.mag && s.reserve > 0) StartReload(d);

        bool trigger = d.auto ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
        if (trigger && !IsReloading && Time.time >= nextShot && !locked)
        {
            if (s.ammo > 0) Fire(s);
            else if (s.reserve > 0) StartReload(d);
            else { nextShot = Time.time + 0.4f; Sfx.Play2D("swap", 0.5f); }
        }
    }

    void SwitchTo(int i)
    {
        if (i == cur) return;
        IsReloading = false;
        cur = i;
        nextShot = Time.time + 0.25f;
        ShowModel();
        Sfx.Play2D("swap");
    }

    void StartReload(WeaponDef d) { Sfx.Play2D("reload"); IsReloading = true; reloadEnd = Time.time + d.reload; }

    void FinishReload(Slot s)
    {
        IsReloading = false;
        int take = Mathf.Min(s.def.mag - s.ammo, s.reserve);
        s.ammo += take; s.reserve -= take;
    }

    void Fire(Slot s)
    {
        var d = s.def;
        s.ammo--;
        nextShot = Time.time + d.interval;
        gunKick = d.kick;
        if (muzzle) muzzle.intensity = d.pellets > 1 ? 6f : 3f;
        Sfx.Play2D(d.sfx, 0.7f);
        if (d.projectile)
        {
            gunKick = d.kick;
            GameBootstrap.SpawnPlayerConcussion(ProjectileOrigin(), cam.transform.forward * d.projectileSpeed, d.damage);
            return;
        }
        float spread = d.spread * (IsZoomed ? 0.3f : 1f);
        for (int i = 0; i < d.pellets; i++) Shoot(spread, d.damage, d.tracer, d.range);
    }

    // Spawn shells at the muzzle, unless that point is behind a wall/enemy right in front of us (then start at the camera)
    Vector3 ProjectileOrigin()
    {
        Vector3 m = muzzlePoint ? muzzlePoint.position : cam.transform.position + cam.transform.forward * 0.9f;
        return Physics.Linecast(cam.transform.position, m, ~0, QueryTriggerInteraction.Ignore) ? cam.transform.position + cam.transform.forward * 0.1f : m;
    }

    void Shoot(float spread, float dmg, Color tracer, float range)
    {
        Vector3 dir = cam.transform.forward + cam.transform.right * Random.Range(-spread, spread) + cam.transform.up * Random.Range(-spread, spread);
        Vector3 end = cam.transform.position + dir * range;
        if (Physics.Raycast(cam.transform.position, dir, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore) && hit.collider.transform.root != transform)
        {
            end = hit.point;
            var enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy) { enemy.TakeDamage(dmg); HitMarker = 1f; }
            else GameBootstrap.SpawnSpark(hit.point, hit.normal);
        }
        GameBootstrap.SpawnTracer(muzzlePoint && gun.gameObject.activeSelf ? muzzlePoint.position : cam.transform.position + cam.transform.right * 0.2f - cam.transform.up * 0.1f, end, tracer);
    }

    // Energy sword: a wide, lethal swing with a short lunge toward the target
    void SwordSwing(WeaponDef d)
    {
        nextShot = Time.time + d.interval;
        gunKick = -0.2f;
        Sfx.Play2D(d.sfx);
        impulse += transform.forward * 12f;
        var struck = new System.Collections.Generic.HashSet<Enemy>();
        foreach (var hit in Physics.SphereCastAll(cam.transform.position, 0.8f, cam.transform.forward, d.meleeRange, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.root == transform) continue;
            var enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy && struck.Add(enemy)) { enemy.TakeDamage(d.damage); HitMarker = 1f; }
        }
    }

    // Halo-style quick melee: a short, hard hit on whatever is in front of the camera.
    void Melee()
    {
        nextMelee = Time.time + meleeCooldown;
        gunKick = -0.2f; // lunge the gun forward
        Sfx.Play2D("melee");
        if (Physics.SphereCast(cam.transform.position, 0.4f, cam.transform.forward, out RaycastHit hit, meleeRange, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.transform.root != transform)
        {
            var enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy) { enemy.TakeDamage(meleeDamage); HitMarker = 1f; Sfx.Play2D("hit"); }
            else GameBootstrap.SpawnSpark(hit.point, hit.normal);
        }
    }

    void ThrowGrenade()
    {
        Grenades--;
        Sfx.Play2D("throw");
        GameBootstrap.SpawnGrenade(cam.transform.position + cam.transform.forward * 0.8f, cam.transform.forward * 18f + Vector3.up * 3f);
    }

    public void AddGrenades(int n) { Grenades = Mathf.Min(Grenades + n, 4); }

    // ---------- Pickups ----------

    void Interact()
    {
        Prompt = "";
        Pickup best = null; float bestD = 2.3f * 2.3f;
        foreach (var p in Pickup.All)
        {
            if (p.kind == Pickup.Kind.Health) continue;
            // In the last stand Noble Six only has the assault rifle and Magnum: nothing else can be picked up
            if (GameBootstrap.FinalStand && p.kind == Pickup.Kind.Weapon && p.weapon != Weapons.AssaultRifle && p.weapon != Weapons.Pistol) continue;
            float d = (p.transform.position - transform.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = p; }
        }
        if (!best) return;
        if (best.kind == Pickup.Kind.DropShield)
        {
            Prompt = HasDropShield ? "" : "[E] TAKE DROP SHIELD   (X to deploy)";
            if (!HasDropShield && Input.GetKeyDown(KeyCode.E)) { HasDropShield = true; Destroy(best.gameObject); Sfx.Play2D("pickup"); }
            return;
        }
        bool sameWeapon = false;
        foreach (var s in slots) if (s.def == best.weapon) sameWeapon = true;
        Prompt = (sameWeapon ? "[E] TAKE AMMO  " : "[E] SWAP  ") + best.weapon.name;
        if (Input.GetKeyDown(KeyCode.E)) { TakeWeapon(best.weapon, best.ammo, best.reserve); Destroy(best.gameObject); Sfx.Play2D("pickup"); }
    }

    // Same weapon: top up reserve. Different weapon: swap it into the current slot and drop the old one at your feet.
    public void TakeWeapon(WeaponDef def, int ammo, int reserve)
    {
        foreach (var s in slots)
            if (s.def == def) { s.reserve = Mathf.Min(s.reserve + ammo + reserve, def.reserve + def.mag * 2); return; }

        var old = slots[cur];
        GameBootstrap.SpawnWeaponPickup(old.def, old.ammo, old.reserve, transform.position + transform.forward * 1.8f, true);
        slots[cur] = new Slot { def = def, ammo = ammo, reserve = reserve };
        IsReloading = false; nextShot = Time.time + 0.3f;
        ShowModel();
    }

    // The last stand: Noble Six is down to a plain assault rifle and Magnum
    public void StripLoadout()
    {
        slots[0] = NewSlot(Weapons.AssaultRifle); slots[1] = NewSlot(Weapons.Pistol);
        cur = 0; IsReloading = false; HasDropShield = false;
        ShowModel();
        Sfx.Play2D("swap");
    }

    public void Heal(float amount) { Health = Mathf.Min(maxHealth, Health + amount); }

    // ---------- Damage ----------

    // Knockback from concussion blasts
    public void AddImpulse(Vector3 v) { impulse += v; }

    // Shield absorbs first; overflow hits health. Any damage resets the recharge timer.
    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        amount *= GameBootstrap.DamageMultiplier;
        if (DropShield.Covers(transform.position)) amount *= 0.3f;
        lastDamageTime = Time.time;
        HitFlash = 1f;
        rechargeSfx = false;
        bool hadShield = Shield > 0f;
        float absorbed = Mathf.Min(Shield, amount);
        Shield -= absorbed;
        Health -= amount - absorbed;
        Sfx.Play2D(hadShield && Shield <= 0f ? "shielddown" : "hurt", 0.8f);
        if (Health <= 0f) Die();
    }

    void Die()
    {
        Health = 0f; IsDead = true; IsSprinting = false; IsZoomed = false;
        cam.fieldOfView = baseFov;
        if (gun) gun.gameObject.SetActive(false);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        GameBootstrap.OnPlayerDeath(this, cam);
    }

    void RechargeShield()
    {
        bool recharging = !GameBootstrap.FinalStand && Time.time - lastDamageTime >= shieldRechargeDelay && Shield < maxShield;
        if (recharging && !rechargeSfx) Sfx.Play2D("recharge", 0.5f);
        rechargeSfx = recharging;
        if (recharging) Shield = Mathf.Min(maxShield, Shield + shieldRechargeRate * Time.deltaTime);
    }
}
