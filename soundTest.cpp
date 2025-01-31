// soundTest.cpp : Этот файл содержит функцию "main". Здесь начинается и заканчивается выполнение программы.
//
#include <windows.h>
#include <mmdeviceapi.h>
#include <audioclient.h>
#include <endpointvolume.h>
#include <iostream>
#include <vector>
#include <comdef.h>
#include <cmath>
#include <fftw3.h>
#include <algorithm>
#include <mfapi.h>
#include <mfidl.h>
#include <mfobjects.h>
#include <mfplay.h>
#include <propkey.h> 
#include <Functiondiscoverykeys_devpkey.h>
const CLSID CLSID_MMDeviceEnumerator = __uuidof(MMDeviceEnumerator);
const IID IID_IMMDeviceEnumerator = __uuidof(IMMDeviceEnumerator);

void ProcessAudioData(BYTE* processedData, BYTE* buffer, UINT32 numFrames, UINT32 numChannels,UINT32 sampleRate, float gain) {
    std::vector<float> processingData;
    processingData.resize(numFrames*numChannels*sizeof(short));
    int N = processingData.size();
    short* pcmData = reinterpret_cast<short*>(buffer);
    for (UINT32 i = 0; i < N; ++i) {
        processingData[i] = pcmData[i] / 32768.0f; // Преобразуем 16-битные значения в float
    }
    fftwf_complex* fftData = (fftwf_complex*)fftwf_malloc(sizeof(fftwf_complex) * N);
    fftwf_plan planForward = fftwf_plan_dft_r2c_1d(N, processingData.data(), fftData, FFTW_ESTIMATE);
    //todo пофиксить баг
    //// Применяем FFT
    fftwf_execute(planForward);
    /*for (int i = 0; i < N / 2 + 1; ++i) {
        float freq = (float)i * sampleRate / N;
        if (freq < 1000) {
            fftData[i][0] *= gain;  // Усиливаем реальную часть
            fftData[i][1] *= gain;  // Усиливаем мнимую часть
        }
    }*/
    fftwf_plan planBackward = fftwf_plan_dft_c2r_1d(N, fftData, processingData.data(), FFTW_ESTIMATE);
    fftwf_execute(planBackward);
    fftwf_destroy_plan(planForward);
    fftwf_destroy_plan(planBackward);
    fftwf_free(fftData);
    for (int i = 0; i < N; ++i) {
        processingData[i] /= N;
    }
    short* processedPcmData = reinterpret_cast<short*>(processedData);
    // Преобразуем данные обратно в 16-битные целые числа, с учетом нормализации в диапазоне [-1, 1]
    for (size_t i = 0; i < processingData.size(); ++i) {
        processedPcmData[i] = static_cast<short>(std::clamp(processingData[i]* 32767.0f, -32768.0f, 32767.0f));
    }
}
HRESULT GetVACDevice(IMMDevice** ppDevice)
{
    HRESULT hr = S_OK;
    IMMDeviceEnumerator* pEnumerator = nullptr;
    IMMDeviceCollection* pCollection = nullptr;
    UINT deviceCount = 0;
    IMMDevice* pDevice = nullptr;
    LPWSTR deviceName;

    // Инициализация COM
    hr = CoInitialize(nullptr);
    if (FAILED(hr)) {
        std::cout << "COM initialization failed!" << std::endl;
        return hr;
    }

    // Создание объекта для перечисления устройств
    hr = CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_ALL, IID_PPV_ARGS(&pEnumerator));
    if (FAILED(hr)) {
        std::cout << "Failed to create IMMDeviceEnumerator!" << std::endl;
        CoUninitialize();
        return hr;
    }

    // Получение списка всех аудиоустройств
    hr = pEnumerator->EnumAudioEndpoints(eRender, DEVICE_STATE_ACTIVE, &pCollection);
    if (FAILED(hr)) {
        std::cout << "Failed to enumerate audio devices!" << std::endl;
        pEnumerator->Release();
        CoUninitialize();
        return hr;
    }

    // Получаем количество устройств
    hr = pCollection->GetCount(&deviceCount);
    if (FAILED(hr)) {
        std::cout << "Failed to get device count!" << std::endl;
        pCollection->Release();
        pEnumerator->Release();
        CoUninitialize();
        return hr;
    }

    // Перебор устройств
    for (UINT i = 0; i < deviceCount; i++) {
        hr = pCollection->Item(i, &pDevice);
        if (FAILED(hr)) {
            std::cout << "Failed to get device!" << std::endl;
            continue;
        }

        // Получение имени устройства
        IPropertyStore* pPropertyStore = nullptr;
        PROPVARIANT prop;
        PropVariantInit(&prop);
        hr = pDevice->OpenPropertyStore(STGM_READ, &pPropertyStore);
        if (FAILED(hr)) {
            pDevice->Release();
            continue;
        }

        hr = pPropertyStore->GetValue(PKEY_Device_FriendlyName, &prop);
        if (FAILED(hr) || prop.vt == VT_EMPTY) {
            pPropertyStore->Release();
            pDevice->Release();
            continue;
        }
        // Проверяем имя устройства (например, "Virtual Audio Cable")
        if (wcsstr(prop.pwszVal, L"Virtual Audio Cable") != nullptr) {
            *ppDevice = pDevice;
            pPropertyStore->Release();
            pEnumerator->Release();
            pCollection->Release();
            CoUninitialize();
            return S_OK; // Найдено виртуальное аудиоустройство VAC
        }

        pPropertyStore->Release();
        pDevice->Release();
    }

    // Если устройство не найдено
    pEnumerator->Release();
    pCollection->Release();
    CoUninitialize();
    return E_FAIL; // Устройство не найдено
}
HRESULT GetRenderDevice(IMMDevice** ppDevice)
{
    HRESULT hr = S_OK;
    IMMDeviceEnumerator* pEnumerator = nullptr;
    IMMDeviceCollection* pCollection = nullptr;
    UINT deviceCount = 0;
    IMMDevice* pDevice = nullptr;

    // Инициализация COM
    hr = CoInitialize(nullptr);
    if (FAILED(hr)) {
        std::cout << "COM initialization failed!" << std::endl;
        return hr;
    }

    // Создание объекта для перечисления устройств
    hr = CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_ALL, IID_PPV_ARGS(&pEnumerator));
    if (FAILED(hr)) {
        std::cout << "Failed to create IMMDeviceEnumerator!" << std::endl;
        CoUninitialize();
        return hr;
    }

    // Получение списка всех аудиоустройств
    hr = pEnumerator->EnumAudioEndpoints(eRender, DEVICE_STATE_ACTIVE, &pCollection);
    if (FAILED(hr)) {
        std::cout << "Failed to enumerate audio devices!" << std::endl;
        pEnumerator->Release();
        CoUninitialize();
        return hr;
    }

    // Получаем количество устройств
    hr = pCollection->GetCount(&deviceCount);
    if (FAILED(hr)) {
        std::cout << "Failed to get device count!" << std::endl;
        pCollection->Release();
        pEnumerator->Release();
        CoUninitialize();
        return hr;
    }

    // Перебор устройств
    for (UINT i = 0; i < deviceCount; i++) {
        hr = pCollection->Item(i, &pDevice);
        if (FAILED(hr)) {
            std::cout << "Failed to get device!" << std::endl;
            continue;
        }

        // Получение имени устройства
        IPropertyStore* pPropertyStore = nullptr;
        PROPVARIANT prop;
        PropVariantInit(&prop);
        hr = pDevice->OpenPropertyStore(STGM_READ, &pPropertyStore);
        if (FAILED(hr)) {
            pDevice->Release();
            continue;
        }

        hr = pPropertyStore->GetValue(PKEY_Device_FriendlyName, &prop);
        if (FAILED(hr) || prop.vt == VT_EMPTY) {
            pPropertyStore->Release();
            pDevice->Release();
            continue;
        }
        // Проверяем имя устройства (например, "Virtual Audio Cable")
        if (wcsstr(prop.pwszVal, L"AirPods Pro") != nullptr) {
            *ppDevice = pDevice;
            pPropertyStore->Release();
            pEnumerator->Release();
            pCollection->Release();
            CoUninitialize();
            return S_OK; // Найдено виртуальное аудиоустройство VAC
        }

        pPropertyStore->Release();
        pDevice->Release();
    }

    // Если устройство не найдено
    pEnumerator->Release();
    pCollection->Release();
    CoUninitialize();
    return E_FAIL; // Устройство не найдено
}
void GetDefaultAudioEndpointDevice(IMMDevice** pDevice)
{
    HRESULT hr = CoInitialize(nullptr);
    if (FAILED(hr)) {
        std::cerr << "CoInitialize failed!" << std::endl;
        return ;
    }

    // Получаем интерфейс устройства вывода
    IMMDeviceEnumerator* pDeviceEnumerator = nullptr;
    hr = CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_ALL, IID_PPV_ARGS(&pDeviceEnumerator));
    if (FAILED(hr)) {
        std::cerr << "Failed to create device enumerator!" << std::endl;
        CoUninitialize();
        return ;
    }

    // Получаем стандартное устройство вывода (динамики или наушники)
    hr = pDeviceEnumerator->GetDefaultAudioEndpoint(eRender, eConsole, pDevice);
    if (FAILED(hr)) {
        std::cerr << "Failed to get default audio endpoint!" << std::endl;
        pDeviceEnumerator->Release();
        CoUninitialize();
        return ;
    }
    pDeviceEnumerator->Release();
}


int main()
{
    IMMDevice* pDevice = nullptr;
    //GetDefaultAudioEndpointDevice(&pDevice);
    GetRenderDevice(&pDevice);
    IMMDevice* pVACDevice = nullptr;
    GetVACDevice(&pVACDevice);

#pragma region capture client
    IAudioClient* pAudioClient = nullptr;
    HRESULT hr = pVACDevice->Activate(__uuidof(IAudioClient), CLSCTX_ALL, nullptr, reinterpret_cast<void**>(&pAudioClient));
    if (FAILED(hr)) {
        std::cerr << "Failed to activate audio client!" << std::endl;
        pVACDevice->Release();
        CoUninitialize();
        return 0;
    }
    WAVEFORMATEX* pFormat = nullptr;
    hr = pAudioClient->GetMixFormat(&pFormat);
    if (FAILED(hr)) {
        std::cerr << "Failed to get mix format!" << std::endl;
        pAudioClient->Release();
        pVACDevice->Release();
        CoUninitialize();
        return 0;
    }
    hr = pAudioClient->Initialize(AUDCLNT_SHAREMODE_SHARED, AUDCLNT_STREAMFLAGS_LOOPBACK, 10000000, 0, pFormat, nullptr);
    if (FAILED(hr)) {
        std::cerr << "Failed to initialize audio client!" << std::endl;
        pAudioClient->Release();
        pVACDevice->Release();
        CoUninitialize();
        return 0;
    }
    IAudioCaptureClient* pCaptureClient = nullptr;
    hr = pAudioClient->GetService(IID_PPV_ARGS(&pCaptureClient));
    if (FAILED(hr)) {
        std::cerr << "Failed to get capture client!" << std::endl;
        pAudioClient->Release();
        pVACDevice->Release();
        CoUninitialize();
        return 0;
    }
#pragma endregion
#pragma region render client
    IAudioClient* pAudioClientRender = nullptr;
    hr = pDevice->Activate(__uuidof(IAudioClient), CLSCTX_ALL, nullptr, reinterpret_cast<void**>(&pAudioClientRender));
    if (FAILED(hr)) {
        std::cerr << "Failed to activate audio client!" << std::endl;
        pDevice->Release();
        CoUninitialize();
        return 0;
    }
    WAVEFORMATEX* pFormatRender = nullptr;
    hr = pAudioClientRender->GetMixFormat(&pFormatRender);
    if (FAILED(hr)) {
        std::cerr << "Failed to get mix format!" << std::endl;
        pAudioClientRender->Release();
        pDevice->Release();
        CoUninitialize();
        return 0;
    }
    hr = pAudioClientRender->Initialize(AUDCLNT_SHAREMODE_SHARED, 0, 10000000, 0, pFormatRender, nullptr);
    if (FAILED(hr)) {
        std::cerr << "Failed to initialize audio client!" << std::endl;
        pAudioClient->Release();
        pDevice->Release();
        CoUninitialize();
        return 0;
    }
    IAudioRenderClient* pAudioRender = nullptr;
    hr = pAudioClientRender->GetService(IID_PPV_ARGS(&pAudioRender));
    if (FAILED(hr)) {
        std::cerr << "Failed to get capture client!" << std::endl;
        pAudioClientRender->Release();
        pDevice->Release();
        CoUninitialize();
        return 0;
    }
#pragma endregion

    hr = pAudioClient->Start();
    if (FAILED(hr)) {
        std::cerr << "Failed to start audio capture!" << std::endl;
        pCaptureClient->Release();
        pAudioClient->Release();
        pDevice->Release();
        CoUninitialize();
        return 0;
    }
    hr = pAudioClientRender->Start();
    if (FAILED(hr)) {
        std::cerr << "Failed to start audio render!" << std::endl;
        pCaptureClient->Release();
        pAudioClient->Release();
        pDevice->Release();
        CoUninitialize();
        return 0;
    }
    BYTE* pData = nullptr;
    UINT32 numFramesAvailable;
    DWORD flags;

    //std::vector<BYTE> processedData;
    //for (int i = 0; i < 1000; ++i) {  // Пример 100 итераций
    while (true)
    {
        hr = pCaptureClient->GetBuffer(&pData, &numFramesAvailable, &flags, nullptr, nullptr);
        if (FAILED(hr)) {
            std::cerr << "Failed to get buffer!" << std::endl;
            break;
        }
        if (pData != nullptr)
        {
            // Применение обработки (например, усиление)
            BYTE* pRenderData = nullptr;
            hr = pAudioRender->GetBuffer(numFramesAvailable, &pRenderData);
            if (SUCCEEDED(hr))
            {
               // memcpy(pRenderData, pData, numFramesAvailable * pFormat->nChannels * sizeof(float));
                ProcessAudioData(pRenderData, pData, numFramesAvailable, pFormat->nChannels, pFormat->nSamplesPerSec, 1.5f);  // Увеличение громкости на 1.5x
                hr = pAudioRender->ReleaseBuffer(numFramesAvailable, 0);
                if (FAILED(hr)) {
                    std::cerr << "Failed to release render buffer!" << std::endl;
                    break;
                }
            }
        }// Отправляем обработанные данные на вывод
       

        // Освобождаем буфер захвата
        hr = pCaptureClient->ReleaseBuffer(numFramesAvailable);
        if (FAILED(hr)) {
            std::cerr << "Failed to release buffer!" << std::endl;
            break;
        }
    }
    pAudioClient->Stop();
    pCaptureClient->Release();
    pAudioClient->Release();
    pDevice->Release();

    // Завершаем COM
    CoUninitialize();
    return 0;
}

