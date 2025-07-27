#! /usr/bin/env pwsh

# Check if ffmpeg is installed
if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) {
    Write-Error "ffmpeg is not installed. Please install ffmpeg to run this script."
    exit 1
}

$timecode="00:00:00:00"
$timecode_drop="00:00:00;00"

$progressiveFps = @("24000/1000", "25000/1000", "30000/1000", "48000/1000", "50000/1000", "60000/1000")
$interlacedFps = @("25000/1000", "30000/1000")
$dropFrameProgressiveFps = @("30000/1001", "60000/1001")
$dropFrameInterlacedFps = @("30000/1001")

$smpteHdBars = "smptehdbars=rate={0}:size=1920x1080"
$sineAudio = "sine=frequency=1000:sample_rate=48000"

foreach ($fps in $progressiveFps) {
    # Calculate frame rate as a double for ffmpeg
    $fpsVal = [int]$fps.Split('/')[0] / [int]$fps.Split('/')[1]
    $fileName = "test_$($fpsVal.ToString("0.00"))p.mxf"
    $ffmpegCMD = "ffmpeg -v error -stats -f lavfi -i `"$($smpteHdBars -f $fps)`" -f lavfi -i `"$($sineAudio)`" -vf `"drawtext=text='Progressive $fps':rate=$($fps):x=(w-tw)/2:y=(h-lh)/2:fontsize=48:fontcolor=white:box=1:boxcolor=black`" -r $fps -c:v mpeg2video -g 12 -pix_fmt yuv422p -color_range 1 -non_linear_quant 1 -dc 10 -intra_vlc 1 -q:v 2 -qmin 2 -qmax 12 -lmin `"1*QP2LAMBDA`" -rc_max_vbv_use 1 -rc_min_vbv_use 1 -b:v 50000k -minrate 50000k -maxrate 50000k -bufsize 17825792 -rc_init_occupancy 17825792 -sc_threshold 1000000000 -bf 2 -c:a pcm_s24le -ar 48000 -ac 2 -f mxf -t 10 -timecode `"$($timecode)`" -y `"$($fileName)`""
    Write-Host "Running: $ffmpegCMD"
    Invoke-Expression $ffmpegCMD
    Write-Host "Created $fileName with $fps fps"
}

foreach ($fps in $interlacedFps) {
    # Calculate frame rate as a double for ffmpeg
    $fpsVal = [int]$fps.Split('/')[0] / [int]$fps.Split('/')[1]
    $fileName = "test_$($fpsVal.ToString("0.00"))i.mxf"
    $ffmpegCMD = "ffmpeg -v error -stats -f lavfi -i `"$($smpteHdBars -f $fps)`" -f lavfi -i `"$($sineAudio)`" -vf `"drawtext=text='Interlaced $fps':rate=$($fps):x=(w-tw)/2:y=(h-lh)/2:fontsize=48:fontcolor=white:box=1:boxcolor=black,format=yuv422p,setfield=tff`" -r $fps -c:v mpeg2video -g 12 -pix_fmt yuv422p -color_range 1 -non_linear_quant 1 -dc 10 -intra_vlc 1 -q:v 2 -qmin 2 -qmax 12 -lmin `"1*QP2LAMBDA`" -rc_max_vbv_use 1 -rc_min_vbv_use 1 -b:v 50000k -minrate 50000k -maxrate 50000k -bufsize 17825792 -rc_init_occupancy 17825792 -sc_threshold 1000000000 -bf 2 -flags +ildct+ilme -top 1 -c:a pcm_s24le -ar 48000 -ac 2 -f mxf -t 10 -timecode `"$($timecode)`" -y `"$($fileName)`""
    Write-Host "Running: $ffmpegCMD"
    Invoke-Expression $ffmpegCMD
    Write-Host "Created $fileName with $fps fps"
}

foreach ($fps in $dropFrameProgressiveFps) {
    # Calculate frame rate as a double for ffmpeg
    $fpsVal = [int]$fps.Split('/')[0] / [int]$fps.Split('/')[1]
    $fileName = "test_$($fpsVal.ToString("0.00"))p_drop.mxf"
    $ffmpegCMD = "ffmpeg -v error -stats -f lavfi -i `"$($smpteHdBars -f $fps)`" -f lavfi -i `"$($sineAudio)`" -vf `"drawtext=text='Drop Frame Progressive $fps':rate=$($fps):x=(w-tw)/2:y=(h-lh)/2:fontsize=48:fontcolor=white:box=1:boxcolor=black`" -r $fps -c:v mpeg2video -g 12 -pix_fmt yuv422p -color_range 1 -non_linear_quant 1 -dc 10 -intra_vlc 1 -q:v 2 -qmin 2 -qmax 12 -lmin `"1*QP2LAMBDA`" -rc_max_vbv_use 1 -rc_min_vbv_use 1 -b:v 50000k -minrate 50000k -maxrate 50000k -bufsize 17825792 -rc_init_occupancy 17825792 -sc_threshold 1000000000 -bf 2 -c:a pcm_s24le -ar 48000 -ac 2 -f mxf -t 10 -timecode `"$($timecode_drop)`" -y `"$($fileName)`""
    Write-Host "Running: $ffmpegCMD"
    Invoke-Expression $ffmpegCMD
    Write-Host "Created $fileName with $fps fps"
}

foreach ($fps in $dropFrameInterlacedFps) {
    # Calculate frame rate as a double for ffmpeg
    $fpsVal = [int]$fps.Split('/')[0] / [int]$fps.Split('/')[1]
    $fileName = "test_$($fpsVal.ToString("0.00"))i_drop.mxf"
    $ffmpegCMD = "ffmpeg -v error -stats -f lavfi -i `"$($smpteHdBars -f $fps)`" -f lavfi -i `"$($sineAudio)`" -vf `"drawtext=text='Drop Frame Interlaced $fps':rate=$($fps):x=(w-tw)/2:y=(h-lh)/2:fontsize=48:fontcolor=white:box=1:boxcolor=black,format=yuv422p,setfield=tff`" -r $fps -c:v mpeg2video -g 12 -pix_fmt yuv422p -color_range 1 -non_linear_quant 1 -dc 10 -intra_vlc 1 -q:v 2 -qmin 2 -qmax 12 -lmin `"1*QP2LAMBDA`" -rc_max_vbv_use 1 -rc_min_vbv_use 1 -b:v 50000k -minrate 50000k -maxrate 50000k -bufsize 17825792 -rc_init_occupancy 17825792 -sc_threshold 1000000000 -bf 2 -flags +ildct+ilme -top 1 -c:a pcm_s24le -ar 48000 -ac 2 -f mxf -t 10 -timecode `"$($timecode_drop)`" -y `"$($fileName)`""
    Write-Host "Running: $ffmpegCMD"
    Invoke-Expression $ffmpegCMD
    Write-Host "Created $fileName with $fps fps"
}