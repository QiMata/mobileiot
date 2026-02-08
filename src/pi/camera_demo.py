"""HTTP camera server for Raspberry Pi Camera Module.

Serves camera frames and optional classification results over HTTP.

Endpoints:
  GET /frame     - Single JPEG snapshot
  GET /classify  - Classify current frame (uses MobileNetV2 TFLite)
  GET /stream    - MJPEG continuous stream
  GET /health    - Server health check

Prerequisites:
  pip install flask picamera2 numpy Pillow
  Optional for classification: pip install tflite-runtime

Run:
  python3 camera_demo.py
"""

import io
import time

from flask import Flask, Response, jsonify
from picamera2 import Picamera2
import numpy as np
from PIL import Image

app = Flask(__name__)

# Initialize camera
camera = Picamera2()
camera.configure(camera.create_still_configuration(main={"size": (640, 480)}))
camera.start()
time.sleep(1)  # warm-up

# Optional: TFLite model for on-device classification
_interpreter = None
_labels = []


def load_model():
    global _interpreter, _labels
    try:
        import tflite_runtime.interpreter as tflite
        _interpreter = tflite.Interpreter(model_path="mobilenet_v2.tflite")
        _interpreter.allocate_tensors()
        with open("imagenet_labels.txt") as f:
            _labels = [line.strip() for line in f.readlines()]
        print(f"TFLite model loaded ({len(_labels)} labels)")
    except Exception as e:
        print(f"TFLite model not available: {e}")
        print("Classification endpoint will return 503")


load_model()


def capture_jpeg():
    """Capture a single JPEG frame from the camera."""
    stream = io.BytesIO()
    camera.capture_file(stream, format='jpeg')
    stream.seek(0)
    return stream.read()


@app.route('/frame')
def frame():
    """Return a single JPEG snapshot."""
    jpeg = capture_jpeg()
    return Response(jpeg, mimetype='image/jpeg')


@app.route('/classify')
def classify():
    """Classify the current camera frame using TFLite model."""
    if _interpreter is None:
        return jsonify({"error": "Model not loaded"}), 503

    jpeg = capture_jpeg()
    img = Image.open(io.BytesIO(jpeg)).resize((224, 224))
    arr = np.array(img, dtype=np.float32) / 255.0
    arr = np.expand_dims(arr, axis=0)

    input_details = _interpreter.get_input_details()
    output_details = _interpreter.get_output_details()
    _interpreter.set_tensor(input_details[0]['index'], arr)
    _interpreter.invoke()
    output = _interpreter.get_tensor(output_details[0]['index'])[0]

    idx = int(np.argmax(output))
    label = _labels[idx] if idx < len(_labels) else f"class_{idx}"
    conf = float(output[idx])
    return jsonify({"label": label, "confidence": round(conf, 4)})


@app.route('/stream')
def stream():
    """MJPEG continuous stream."""
    def generate():
        while True:
            jpeg = capture_jpeg()
            yield (b'--frame\r\n'
                   b'Content-Type: image/jpeg\r\n\r\n' + jpeg + b'\r\n')
            time.sleep(0.1)
    return Response(generate(), mimetype='multipart/x-mixed-replace; boundary=frame')


@app.route('/health')
def health():
    """Health check endpoint."""
    return jsonify({"status": "ok", "resolution": "640x480"})


if __name__ == "__main__":
    print("Pi Camera server starting on port 5000...")
    app.run(host="0.0.0.0", port=5000)
