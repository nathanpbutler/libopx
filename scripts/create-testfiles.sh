#!/bin/bash

# Check if ffmpeg is installed
if ! command -v ffmpeg &> /dev/null; then
    echo "ffmpeg could not be found. Please install ffmpeg to run this script."
    exit 1
fi

timecode="00:00:00:00"
timecode_drop="00:00:00;00"

progressive_fps=("24000/1000" "25000/1000" "30000/1000" "48000/1000" "50000/1000" "60000/1000")
interlaced_fps=("25000/1000" "30000/1000")
drop_frame_progressive_fps=("30000/1001" "60000/1001")
drop_frame_interlaced_fps=("30000/1001")

smpte_hd_bars="smptehdbars=rate=%s:size=1920x1080"
sine_audio="sine=frequency=1000:sample_rate=48000"

# Function to calculate fps value for filename
calc_fps() {
    local fps=$1
    local numerator=$(echo $fps | cut -d'/' -f1)
    local denominator=$(echo $fps | cut -d'/' -f2)
    echo "scale=2; $numerator / $denominator" | bc
}

# Progressive frame rates
for fps in "${progressive_fps[@]}"; do
    fps_val=$(calc_fps $fps)
    filename="test_${fps_val}p.mxf"
    echo "Running ffmpeg for $fps (${fps_val}p)..."
    ffmpeg -v error -stats -f lavfi -i "$(printf "$smpte_hd_bars" "$fps")" \
    -f lavfi -i "$sine_audio" \
    -vf "drawtext=text='Progressive $fps':rate=$fps:x=(w-tw)/2:y=(h-lh)/2:fontsize=48:fontcolor=white:box=1:boxcolor=black" \
    -r $fps -c:v mpeg2video -g 12 -pix_fmt yuv422p -color_range 1 -non_linear_quant 1 -dc 10 -intra_vlc 1 -q:v 2 -qmin 2 -qmax 12 -lmin "1*QP2LAMBDA" -rc_max_vbv_use 1 -rc_min_vbv_use 1 -b:v 50000k -minrate 50000k -maxrate 50000k -bufsize 17825792 -rc_init_occupancy 17825792 -sc_threshold 1000000000 -bf 2 \
    -c:a pcm_s24le -ar 48000 -ac 2 -f mxf -t 10 -timecode "$timecode" -y "$filename"
    echo "Created $filename with $fps fps"
done

# Interlaced frame rates
for fps in "${interlaced_fps[@]}"; do
    fps_val=$(calc_fps $fps)
    filename="test_${fps_val}i.mxf"
    echo "Running ffmpeg for $fps (${fps_val}i)..."
    ffmpeg -v error -stats -f lavfi -i "$(printf "$smpte_hd_bars" "$fps")" \
    -f lavfi -i "$sine_audio" \
    -vf "drawtext=text='Interlaced $fps':rate=$fps:x=(w-tw)/2:y=(h-lh)/2:fontsize=48:fontcolor=white:box=1:boxcolor=black,format=yuv422p,setfield=tff" \
    -r $fps -c:v mpeg2video -g 12 -pix_fmt yuv422p -color_range 1 -non_linear_quant 1 -dc 10 -intra_vlc 1 -q:v 2 -qmin 2 -qmax 12 -lmin "1*QP2LAMBDA" -rc_max_vbv_use 1 -rc_min_vbv_use 1 -b:v 50000k -minrate 50000k -maxrate 50000k -bufsize 17825792 -rc_init_occupancy 17825792 -sc_threshold 1000000000 -bf 2 -flags +ildct+ilme -top 1 \
    -c:a pcm_s24le -ar 48000 -ac 2 -f mxf -t 10 -timecode "$timecode" -y "$filename"
    echo "Created $filename with $fps fps"
done

# Drop frame progressive
for fps in "${drop_frame_progressive_fps[@]}"; do
    fps_val=$(calc_fps $fps)
    filename="test_${fps_val}p_drop.mxf"
    echo "Running ffmpeg for $fps (${fps_val}p drop)..."
    ffmpeg -v error -stats -f lavfi -i "$(printf "$smpte_hd_bars" "$fps")" \
    -f lavfi -i "$sine_audio" \
    -vf "drawtext=text='Drop Frame Progressive $fps':rate=$fps:x=(w-tw)/2:y=(h-lh)/2:fontsize=48:fontcolor=white:box=1:boxcolor=black" \
    -r $fps -c:v mpeg2video -g 12 -pix_fmt yuv422p -color_range 1 -non_linear_quant 1 -dc 10 -intra_vlc 1 -q:v 2 -qmin 2 -qmax 12 -lmin "1*QP2LAMBDA" -rc_max_vbv_use 1 -rc_min_vbv_use 1 -b:v 50000k -minrate 50000k -maxrate 50000k -bufsize 17825792 -rc_init_occupancy 17825792 -sc_threshold 1000000000 -bf 2 \
    -c:a pcm_s24le -ar 48000 -ac 2 -f mxf -t 10 -timecode "$timecode_drop" -y "$filename"
    echo "Created $filename with $fps fps"
done

# Drop frame interlaced
for fps in "${drop_frame_interlaced_fps[@]}"; do
    fps_val=$(calc_fps $fps)
    filename="test_${fps_val}i_drop.mxf"
    echo "Running ffmpeg for $fps (${fps_val}i drop)..."
    ffmpeg -v error -stats -f lavfi -i "$(printf "$smpte_hd_bars" "$fps")" \
    -f lavfi -i "$sine_audio" \
    -vf "drawtext=text='Drop Frame Interlaced $fps':rate=$fps:x=(w-tw)/2:y=(h-lh)/2:fontsize=48:fontcolor=white:box=1:boxcolor=black,format=yuv422p,setfield=tff" \
    -r $fps -c:v mpeg2video -g 12 -pix_fmt yuv422p -color_range 1 -non_linear_quant 1 -dc 10 -intra_vlc 1 -q:v 2 -qmin 2 -qmax 12 -lmin "1*QP2LAMBDA" -rc_max_vbv_use 1 -rc_min_vbv_use 1 -b:v 50000k -minrate 50000k -maxrate 50000k -bufsize 17825792 -rc_init_occupancy 17825792 -sc_threshold 1000000000 -bf 2 -flags +ildct+ilme -top 1 \
    -c:a pcm_s24le -ar 48000 -ac 2 -f mxf -t 10 -timecode "$timecode_drop" -y "$filename"
    echo "Created $filename with $fps fps"
done