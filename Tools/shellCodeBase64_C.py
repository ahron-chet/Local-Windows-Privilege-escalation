def getBaseChunks(baseString):
    p1 = """const char *chunks[] = {
    """
    p2="""\n};\nstd::string base64_encoded;\nfor (const auto& chunk : chunks) {\n\tbase64_encoded += chunk;\n}\n"""
    test = ''
    c = 64
    r = (len(baseString)//64) + 1
    return p1+',\n'.join(
        [f'\t"{baseString[i*c:(i+1)*c]}"' for i in range(0,r)]
    ) + p2
    
    