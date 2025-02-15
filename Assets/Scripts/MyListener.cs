using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Serialization;


public class MyListener : MonoBehaviour
{
    public float throttleIncrement = 0.1f;
    public float maxThrust = 200f;
    public float responsiveness = 10f;
    public float lift = 135f;

    private bool _throttleDown;
    private float _throttle;
    private float _roll;
    private float _pitch;
    private float _yaw;


    [FormerlySerializedAs("_pitchCorrection")] [SerializeField]
    private float pitchCorrection = 0;

    [FormerlySerializedAs("_rollCorrection")] [SerializeField]
    private float rollCorrection = 0;

    private int _calibrationFrame = 0;
    [SerializeField] private int framesNeeded = 10;

    [SerializeField] private bool useSerial = true;
    [SerializeField] private float joystickDeadzone = 0.05f;
    [SerializeField] private float yawDeadzone = 0.07f;
    private Rigidbody _rb;
    [SerializeField] private TextMeshProUGUI hud;
    [SerializeField] private Transform propeller;

    private float ResponseModifier
    {
        get { return (_rb.mass / 10f) * responsiveness; }
    }

    public float GetThrottle()
    {
        return _throttle;
    }

    // Start is called before the first frame update
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void HandleNonSerialInputs()
    {
        if (!useSerial)
        {
            _roll = Input.GetAxis("Roll");
            _pitch = Input.GetAxis("Pitch");
            _yaw = Input.GetAxis("Yaw");
            _throttleDown = Input.GetKey(KeyCode.Space);
        }
    }

    void OnMessageArrived(string msg)
    {
        /*
         * Function is called each time Unity receives a message from Arduino.
         * msg: the message received.
         */
        if (useSerial)
        {
            var slicedMessage = msg.Split(' ');
            var serialIntegers = new int[slicedMessage.Length];

            for (int i = 0; i < slicedMessage.Length; i++)
            {
                serialIntegers[i] = int.Parse(slicedMessage[i]);
            }

            if (_calibrationFrame < framesNeeded)
            {
                hud.text = "Calibrating joystick..." +
                           ((_calibrationFrame * 1f / framesNeeded) * 100f).ToString("F00") + "%";
                pitchCorrection += (serialIntegers[1] / (4095f / 2f) - 1) * 1f / framesNeeded;
                rollCorrection += (serialIntegers[0] / (4095f / 2f) - 1) * 1f / framesNeeded;

                _calibrationFrame++;
            }
            else
            {
                _yaw = serialIntegers[3] / (4095f / 2f) - 1;
                _pitch = serialIntegers[1] / (4095f / 2f) - 1 - pitchCorrection;
                _roll = serialIntegers[0] / (4095f / 2f) - 1 - rollCorrection;
                _throttleDown = serialIntegers[2] != 1;
                // ignore extremely small values -- they are most likely unintentional
                if (Math.Abs(_pitch) < joystickDeadzone)
                    _pitch = 0f;
                if (Math.Abs(_roll) < joystickDeadzone)
                    _roll = 0f;
                if (Math.Abs(_yaw) < yawDeadzone)
                    _yaw = 0f;

                // Debug.Log($"yaw: {_yaw}, pitch: {_pitch}, roll: {_roll}");
            }
        }
    }

    void OnConnectionEvent(bool success)
    {
        if (useSerial)
            Debug.Log(success ? "Device connected!" : "Device DISCONNECTED.");
    }

    // Update is called once per frame
    void Update()
    {
        if (_calibrationFrame >= framesNeeded || !useSerial)
        {
            HandleNonSerialInputs();
            _throttle = UpdateThrottle();
            _throttle = Mathf.Clamp(_throttle, 0f, 100f);
            
            UpdateHUD();
            if (_throttle > 0)
            {
                propeller.Rotate(Vector3.right * _throttle);
            }
        }
    }

    private void FixedUpdate()
    {
        if (_calibrationFrame >= framesNeeded || !useSerial)
        {
            _rb.useGravity = true;
            _rb.AddForce(transform.right * (maxThrust * _throttle));
            _rb.AddTorque(transform.up * (_yaw * ResponseModifier));
            _rb.AddTorque(-transform.right * (_roll * ResponseModifier));
            _rb.AddTorque(transform.forward * (_pitch * ResponseModifier));

            _rb.AddForce(Vector3.up * (_rb.velocity.magnitude * lift));
        }

        else
        {
            _rb.useGravity = false;
        }
    }

    private void UpdateHUD()
    {
        hud.text = "";
        hud.text += "Throttle: " + _throttle.ToString("F0") + "%\n";
        hud.text += "Airspeed: " + (_rb.velocity.magnitude * 3.6f).ToString("F0") + "km/h\n";
        hud.text += "Altitude: " + transform.position.y.ToString("F0") + " m";
    }

    float UpdateThrottle()
    {
        if (_throttleDown)
        {
            return _throttle + throttleIncrement;
        }

        return _throttle - throttleIncrement;
    }
}