# USAGE
# python train.py --dataset dataset --model pokedex.model --labelbin lb.pickle

# set the matplotlib backend so figures can be saved in the background
import matplotlib
matplotlib.use("Agg")

# import the necessary packages
from keras.preprocessing.image import ImageDataGenerator
from keras.optimizers import Adam
from keras.preprocessing.image import img_to_array
from sklearn.preprocessing import LabelBinarizer
from sklearn.model_selection import train_test_split
from imutils import paths
from Builder import Builder
import matplotlib.pyplot as plt
import numpy as np
import argparse
import random
import pickle
import cv2
import os


# initialize the number of epochs to train for, initial learning rate
# batch size, and image dimensions
EPOCHS = 25
INIT_LR = 1e-3
BS = 32
IMAGE_DIMS = (96, 96, 3)

# initialize the data and labels
print("[INFO] loading images...")
data = []
labels = []

# grab the image paths and randomly shuffle them
imagePaths = sorted(list(paths.list_images("..\\Bulbasaur.DataFetcher\\bin\\x64\\Debug\\netcoreapp2.0\\Data")))
random.shuffle(imagePaths)

# loop over the input images
for imagePath in imagePaths:
    try:
        # load the image, pre-process it, and store it in the data list
        image = cv2.imread(imagePath)
        image = cv2.resize(image, (IMAGE_DIMS[0], IMAGE_DIMS[1]))
        image = img_to_array(image)
        data.append(image)

        # extrat the class label fromthe image path and update the labels list
        label = imagePath.split(os.path.sep)[-2]
        labels.append(label)
        pass
    except :
        pass

# scale the raw pixel intensities to the range [0, 1]
data = np.array(data, dtype="float") / 255.0
labels = np.array(labels)
print("[INFO] data matrix: {:.2f}MB".format(data.nbytes / (1024 * 1000.0)))

# binarize the labels
labelBinarizer = LabelBinarizer()
labels = labelBinarizer.fit_transform(labels)

# partition the data into traning and testing splits using 80% of the data for
# training and the remaining 20% for testing
(trainX, testX, trainY, testY) = train_test_split(data, labels, test_size = 0.2, random_state = 42)

dataGen = ImageDataGenerator(rotation_range=25,
    width_shift_range=0.1,
    height_shift_range=0.1,
    # rescale=1./255,
    shear_range=0.2,
    zoom_range=0.2,
    horizontal_flip=True,
    fill_mode='nearest')

print("[INFO] compiling model...")
model = Builder.build(IMAGE_DIMS[0], IMAGE_DIMS[1], IMAGE_DIMS[2], len(labelBinarizer.classes_))
optimizer = Adam(INIT_LR,decay=INIT_LR / EPOCHS)
model.compile(optimizer,"categorical_crossentropy",["accuracy"])

# train the network
print("[INFO] training network...")
H = model.fit_generator(dataGen.flow(trainX,trainY,BS), len(trainX) // BS,EPOCHS, validation_data = (testX, testY))

# save the model to disk
print("[INFO] serializing network...")
model.save("..\\pokedex.model")

# save the label binarizer to disk
print("[INFO] serializing label binarizer...")
f = open("..\\label.pickle", "wb")
f.write(pickle.dumps(lb))
f.close()

# plot the training loss and accuracy
plt.style.use("ggplot")
plt.figure()
N = EPOCHS
plt.plot(np.arange(0, N), H.history["loss"], label="train_loss")
plt.plot(np.arange(0, N), H.history["val_loss"], label="val_loss")
plt.plot(np.arange(0, N), H.history["acc"], label="train_acc")
plt.plot(np.arange(0, N), H.history["val_acc"], label="val_acc")
plt.title("Training Loss and Accuracy")
plt.xlabel("Epoch #")
plt.ylabel("Loss/Accuracy")
plt.legend(loc="upper left")
plt.savefig("..\\plot.png")



## load image
#origin = cv2.imread("..\\Bulbasaur.DataFetcher\\bin\\x64\\Debug\\netcoreapp2.0\\Data\\Abomasnow\\1.png")
#image = cv2.resize(origin, (100, 100))
#image = image.astype("float") / 255.0
#image = img_to_array(image)
#image = np.expand_dims(image, 0)
