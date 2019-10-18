/* カメラを動かすためのJavascript */

var localStream = null;
var ios = /iPad|iPhone|iPod/.test(navigator.userAgent);
var devices;
var activeIndex;
var iosRear = false;
var postCount = 0;

function decodeImageFromBase64(data, callback) {
    qrcode.callback = callback;
    qrcode.decode(data);
}

function decode() {
    if (localStream) {
        var canvas = document.getElementById('canvas');
        var ctx = canvas.getContext('2d');
        var h;
        var w;

        w = video.videoWidth;
        h = video.videoHeight;

        canvas.setAttribute('width', w);
        canvas.setAttribute('height', h);
        ctx.drawImage(video, 0, 0, w, h);

        decodeImageFromBase64(canvas.toDataURL('image/png'), function (decodeInformation) {
            var input = document.getElementById('elemIdStr');
            if (!(decodeInformation instanceof Error)) {
                input.value = decodeInformation;
            }
        });
    }
}

function openQRCamera(node) {
    var reader = new FileReader();
    reader.onload = function () {
        node.value = '';
        qrcode.callback = function (res) {
            if (res instanceof Error) {
                alert('QRコードが見つかりませんでした。QRコードがカメラのフレーム内に収まるよう、再度撮影してください。');
            } else {
                var elemIdStr = document.getElementById('elemIdStr');
                elemIdStr.value = res;
            }
        };

        qrcode.decode(reader.result);
    };

    reader.readAsDataURL(node.files[0]);
}

window.onload = function () {
    var modeChange = function (mode) {
        if (mode === 'camera') {
            document.getElementById('video-input').style.display = 'none';
            document.getElementById('photo-input').style.display = 'block';
            document.getElementById('toCamera').style.display = 'none';
            document.getElementById('toMovie').style.display = 'block';
        } else {
            document.getElementById('video-input').style.display = 'block';
            document.getElementById('photo-input').style.display = 'none';
            document.getElementById('toCamera').style.display = 'block';
            document.getElementById('toMovie').style.display = 'none';
        }
    };

    if (!navigator.mediaDevices || !navigator.mediaDevices.enumerateDevices) {
        modeChange('camera');
        return;
    }

    // カメラ情報取得
    navigator.mediaDevices.enumerateDevices()
        .then(function (cameras) {
            var cams = [];
            cameras.forEach(function (device) {
                if (device.kind === 'videoinput') {
                    cams.push({
                        'id': device.deviceId,
                        'name': device.label
                    });
                }
            });

            devices = cams;
            changeCamera(devices.length - 1);
        })
        .catch(function (err) {
            alert('カメラが見つかりません');
        });

    var video = document.getElementById('video');

    var startReadQR = function () {
        setInterval('decode();', 500);
    };

    var changeCamera = function (index) {
        if (localStream) {
            localStream.getVideoTracks()[0].stop();
        }

        activeIndex = index;
        iosRear = !iosRear;
        var p = document.getElementById('active-camera');
        p.innerHTML = devices[activeIndex].name + '(' + devices[activeIndex].id + ')';
        setCamera();
    };

    var setCamera = function () {
        navigator.getUserMedia = navigator.getUserMedia || navigator.webkitGetUserMedia || windiow.navigator.mozGetUserMedia;
        window.URL = window.URL || window.webkitURL;

        var videoOptions;

        if (ios) {
            videoOptions = {
                facingMode: {
                    exact: (iosRear) ? 'environment' : 'user'
                },
                mandatory: {
                    sourceId: devices[activeIndex].id,
                    minWidth: 600,
                    maxWidth: 800,
                    minAspectRatio: 1.6
                },
                optional: []
            };
        } else {
            videoOptions = {
                mandatory: {
                    sourceId: devices[activeIndex].id,
                    minWidth: 600,
                    maxWidth: 800,
                    minAspectRatio: 1.6
                },
                optional: []
            };
        }

        navigator.getUserMedia(
            {
                audio: false,
                video: videoOptions
            },
            function (stream) {
                if (ios) {
                    video.srcObject = stream;
                } else {
                    video.src = window.URL.createObjectURL(stream);
                }
                localStream = stream;
            },
            function (err) {

            }
        );

        startReadQR();
    };

    document.getElementById('toCamera').addEventListener('click', function () {
        modeChange('camera');
    });

    document.getElementById('toMovie').addEventListener('click', function () {
        modeChange('video');
    });

    document.getElementById('changeCamera').addEventListener('click', function () {
        var newIndex = activeIndex + 1;
        if (newIndex >= devices.length) {
            newIndex = 0;
        }
        changeCamera(newIndex);
    }, false);
};
