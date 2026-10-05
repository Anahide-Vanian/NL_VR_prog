from faster_whisper import WhisperModel                                                                                                                                               


class WhisperService:
    def __init__(self):
        # int8 = fastest CPU quantization; small = same size as before
        self.model = WhisperModel("small", device="cpu", compute_type="int8")                                                                                                         

    def transcribe(self, file_path):                                                                                                                                                  
        segments, info = self.model.transcribe(                                                                                                                                     
            file_path,                                                                                                                                                                
            language="en",
            vad_filter=True,   # skip leading/trailing silence, extra speedup                                                                                                        
        )                                                                                                                                                                           
        text = "".join(seg.text for seg in segments).strip()
        return {                                                                                                                                                                      
            "text": text,
            "language": info.language,                                                                                                                                                
        }   
