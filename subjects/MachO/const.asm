;;; Segment __TEXT,__text (00000000)
00000000 55 89 E5 E8                                     U...           

;; _f: 00000004
_f proc
	add	[eax],al
	add	[eax],al
	pop	eax
	mov	eax,[eax+0000000B]
	mov	eax,[eax]
	pop	ebp
	ret
;;; Segment __IMPORT,__pointers (00000013)
__imp___f		; 00000013
	dd	0x00000000
